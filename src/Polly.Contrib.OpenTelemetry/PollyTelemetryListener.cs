// Copyright © 2025 Justin Bannister. MIT Licence.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Telemetry;
using Polly.Timeout;

namespace Polly.Contrib.OpenTelemetry;

/// <summary>
/// A Polly <see cref="TelemetryListener"/> that emits OpenTelemetry <see cref="Activity"/> spans
/// and <see cref="Meter"/> metrics for resilience pipeline events.
/// </summary>
internal sealed class PollyTelemetryListener : TelemetryListener
{
    private static readonly ActivitySource ActivitySource =
        new(PollyInstrumentation.ActivitySourceName, PollyInstrumentation.ActivitySourceVersion);

    private static readonly Meter Meter =
        new(PollyInstrumentation.MeterName, PollyInstrumentation.MeterVersion);

    private static readonly Histogram<double> AttemptDuration = Meter.CreateHistogram<double>(
        MetricNames.AttemptDuration, "ms",
        "Duration of individual execution attempts within a resilience pipeline.");

    private static readonly Counter<long> RetryCounter = Meter.CreateCounter<long>(
        MetricNames.RetryCount, "{attempt}",
        "Number of retry decisions made by a retry resilience strategy.");

    private static readonly Counter<long> CircuitBreakerOpenCounter = Meter.CreateCounter<long>(
        MetricNames.CircuitBreakerOpenCount, "{open}",
        "Number of times a circuit breaker transitioned to the open state.");

    private static readonly Counter<long> TimeoutCounter = Meter.CreateCounter<long>(
        MetricNames.TimeoutCount, "{timeout}",
        "Number of operations cancelled due to a timeout strategy.");

    private readonly PollyInstrumentationOptions _options;

    internal PollyTelemetryListener(PollyInstrumentationOptions options)
    {
        _options = options;
    }

    /// <inheritdoc/>
    public override void Write<TResult, TArgs>(in TelemetryEventArguments<TResult, TArgs> args)
    {
        switch (args.Event.EventName)
        {
            case "ExecutionAttempt" when args.Arguments is ExecutionAttemptArguments attemptArgs:
                OnExecutionAttempt(attemptArgs, args.Source, args.Outcome, args.Event);
                break;

            case "OnRetry" when args.Arguments is OnRetryArguments<TResult> retryArgs:
                OnRetry(retryArgs, args.Source, args.Outcome, args.Event);
                break;

            case "OnCircuitOpened" when args.Arguments is OnCircuitOpenedArguments<TResult> openArgs:
                OnCircuitOpened(openArgs.BreakDuration, openArgs.IsManual, args.Source, args.Outcome, args.Event);
                break;

            case "OnCircuitClosed":
                OnCircuitClosed(args.Source, args.Event);
                break;

            case "OnCircuitHalfOpened":
                OnCircuitHalfOpened(args.Source, args.Event);
                break;

            case "OnTimeout" when args.Arguments is OnTimeoutArguments timeoutArgs:
                OnTimeout(timeoutArgs, args.Source, args.Event);
                break;
        }
    }

    private void OnExecutionAttempt<TResult>(
        ExecutionAttemptArguments attemptArgs,
        ResilienceTelemetrySource source,
        Outcome<TResult>? outcome,
        ResilienceEvent resilienceEvent)
    {
        var outcomeLabel = GetOutcomeLabel(outcome);

        if (_options.EnableMetrics)
        {
            var tags = BuildTagList(source, outcomeLabel, outcome?.Exception);
            tags.Add(Tags.AttemptNumber, attemptArgs.AttemptNumber);
            AttemptDuration.Record(attemptArgs.Duration.TotalMilliseconds, tags);
        }

        if (_options.EnableTracing && ActivitySource.HasListeners())
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime - attemptArgs.Duration;

            using var activity = ActivitySource.StartActivity(
                ActivityNames.Attempt,
                ActivityKind.Internal,
                Activity.Current?.Context ?? default,
                startTime: startTime);

            if (activity is null) return;

            SetCommonTags(activity, source, outcomeLabel);
            activity.SetTag(Tags.AttemptNumber, attemptArgs.AttemptNumber);
            activity.SetTag(Tags.AttemptHandled, attemptArgs.Handled);

            if (outcome?.Exception is { } ex)
            {
                activity.SetTag(Tags.ErrorType, ex.GetType().FullName);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message },
                }));
            }

            activity.SetEndTime(endTime);
            _options.EnrichActivity?.Invoke(activity, resilienceEvent, source);
        }
    }

    private void OnRetry<TResult>(
        OnRetryArguments<TResult> retryArgs,
        ResilienceTelemetrySource source,
        Outcome<TResult>? outcome,
        ResilienceEvent resilienceEvent)
    {
        var outcomeLabel = GetOutcomeLabel(outcome);

        if (_options.EnableMetrics)
        {
            var tags = BuildTagList(source, outcomeLabel, outcome?.Exception);
            tags.Add(Tags.AttemptNumber, retryArgs.AttemptNumber);
            RetryCounter.Add(1, tags);
        }

        if (_options.EnableTracing && ActivitySource.HasListeners())
        {
            using var activity = ActivitySource.StartActivity(ActivityNames.Retry, ActivityKind.Internal);
            if (activity is null) return;

            SetCommonTags(activity, source, outcomeLabel);
            activity.SetTag(Tags.AttemptNumber, retryArgs.AttemptNumber);
            activity.SetTag(Tags.RetryDelayMs, retryArgs.RetryDelay.TotalMilliseconds);

            if (outcome?.Exception is { } ex)
            {
                activity.SetTag(Tags.ErrorType, ex.GetType().FullName);
            }

            _options.EnrichActivity?.Invoke(activity, resilienceEvent, source);
        }
    }

    private void OnCircuitOpened<TResult>(
        TimeSpan breakDuration,
        bool isManual,
        ResilienceTelemetrySource source,
        Outcome<TResult>? outcome,
        ResilienceEvent resilienceEvent)
    {
        if (_options.EnableMetrics)
        {
            var tags = BuildTagList(source, "open", outcome?.Exception);
            CircuitBreakerOpenCounter.Add(1, tags);
        }

        if (_options.EnableTracing && ActivitySource.HasListeners())
        {
            using var activity = ActivitySource.StartActivity(ActivityNames.CircuitBreakerOpened, ActivityKind.Internal);
            if (activity is null) return;

            SetCommonTags(activity, source, "open");
            activity.SetTag(Tags.CircuitBreakerBreakDurationMs, breakDuration.TotalMilliseconds);
            activity.SetTag(Tags.CircuitBreakerIsManual, isManual);
            activity.SetStatus(ActivityStatusCode.Error, "Circuit breaker opened.");
            _options.EnrichActivity?.Invoke(activity, resilienceEvent, source);
        }
    }

    private void OnCircuitClosed(ResilienceTelemetrySource source, ResilienceEvent resilienceEvent)
    {
        if (_options.EnableTracing && ActivitySource.HasListeners())
        {
            using var activity = ActivitySource.StartActivity(ActivityNames.CircuitBreakerClosed, ActivityKind.Internal);
            if (activity is null) return;

            SetCommonTags(activity, source, "closed");
            _options.EnrichActivity?.Invoke(activity, resilienceEvent, source);
        }
    }

    private void OnCircuitHalfOpened(ResilienceTelemetrySource source, ResilienceEvent resilienceEvent)
    {
        if (_options.EnableTracing && ActivitySource.HasListeners())
        {
            using var activity = ActivitySource.StartActivity(ActivityNames.CircuitBreakerHalfOpened, ActivityKind.Internal);
            if (activity is null) return;

            SetCommonTags(activity, source, "half_open");
            _options.EnrichActivity?.Invoke(activity, resilienceEvent, source);
        }
    }

    private void OnTimeout(
        OnTimeoutArguments timeoutArgs,
        ResilienceTelemetrySource source,
        ResilienceEvent resilienceEvent)
    {
        if (_options.EnableMetrics)
        {
            var tags = BuildTagList(source, "timeout", exception: null);
            TimeoutCounter.Add(1, tags);
        }

        if (_options.EnableTracing && ActivitySource.HasListeners())
        {
            using var activity = ActivitySource.StartActivity(ActivityNames.Timeout, ActivityKind.Internal);
            if (activity is null) return;

            SetCommonTags(activity, source, "timeout");
            activity.SetTag(Tags.TimeoutMs, timeoutArgs.Timeout.TotalMilliseconds);
            activity.SetStatus(ActivityStatusCode.Error, "Operation timed out.");
            _options.EnrichActivity?.Invoke(activity, resilienceEvent, source);
        }
    }

    private static void SetCommonTags(Activity activity, ResilienceTelemetrySource source, string outcome)
    {
        activity.SetTag(Tags.PipelineName, source.PipelineName);
        activity.SetTag(Tags.PipelineInstanceName, source.PipelineInstanceName);
        activity.SetTag(Tags.StrategyName, source.StrategyName);
        activity.SetTag(Tags.Outcome, outcome);
    }

    private static TagList BuildTagList(ResilienceTelemetrySource source, string outcome, Exception? exception)
    {
        var tags = new TagList
        {
            { Tags.PipelineName, source.PipelineName },
            { Tags.PipelineInstanceName, source.PipelineInstanceName },
            { Tags.StrategyName, source.StrategyName },
            { Tags.Outcome, outcome },
        };

        if (exception is not null)
        {
            tags.Add(Tags.ErrorType, exception.GetType().FullName);
        }

        return tags;
    }

    private static string GetOutcomeLabel<TResult>(Outcome<TResult>? outcome)
    {
        if (outcome is null) return "unknown";
        return outcome.Value.Exception is not null ? "failure" : "success";
    }
}
