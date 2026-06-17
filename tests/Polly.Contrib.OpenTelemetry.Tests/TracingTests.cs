// Copyright © 2025 Justin Bannister. MIT Licence.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Contrib.OpenTelemetry;
using Polly.Retry;
using Polly.Timeout;

namespace Polly.Contrib.OpenTelemetry.Tests;

[TestFixture]
public class TracingTests
{
    private List<Activity> _activities = [];
    private ActivityListener? _listener;

    [SetUp]
    public void SetUp()
    {
        _activities = [];
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Polly",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _activities.Add,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [TearDown]
    public void TearDown()
    {
        _listener?.Dispose();
        _activities.Clear();
    }

    [Test]
    public async Task Retry_EmitsAttemptAndRetryActivities()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.Zero,
                BackoffType = DelayBackoffType.Constant,
            })
            .Build();

        var callCount = 0;
        await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            if (callCount < 3) throw new InvalidOperationException("transient");
            return ValueTask.CompletedTask;
        });

        var attemptActivities = _activities.Where(a => a.OperationName == "polly.attempt").ToList();
        var retryActivities = _activities.Where(a => a.OperationName == "polly.retry").ToList();

        attemptActivities.Should().HaveCount(3, "3 attempts (2 failures + 1 success)");
        retryActivities.Should().HaveCount(2, "2 retry decisions");

        var firstAttempt = attemptActivities[0];
        firstAttempt.GetTagItem("polly.attempt.number").Should().Be(0);
        firstAttempt.GetTagItem("polly.outcome").Should().Be("failure");
        firstAttempt.GetTagItem("error.type").Should().Be(typeof(InvalidOperationException).FullName);

        var lastAttempt = attemptActivities[2];
        lastAttempt.GetTagItem("polly.outcome").Should().Be("success");
    }

    [Test]
    public async Task SuccessfulExecution_EmitsSingleAttemptWithSuccessOutcome()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry()
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 1, Delay = TimeSpan.Zero })
            .Build();

        await pipeline.ExecuteAsync(_ => ValueTask.CompletedTask);

        var attempts = _activities.Where(a => a.OperationName == "polly.attempt").ToList();
        attempts.Should().HaveCount(1);
        attempts[0].GetTagItem("polly.outcome").Should().Be("success");
        attempts[0].Status.Should().Be(ActivityStatusCode.Unset);
    }

    [Test]
    public async Task RetryActivity_HasRetryDelayTag()
    {
        var delay = TimeSpan.FromMilliseconds(50);
        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                Delay = delay,
                BackoffType = DelayBackoffType.Constant,
            })
            .Build();

        try { await pipeline.ExecuteAsync(_ => throw new InvalidOperationException()); }
        catch { /* expected */ }

        var retryActivities = _activities.Where(a => a.OperationName == "polly.retry").ToList();
        retryActivities.Should().HaveCount(1);
        retryActivities[0].GetTagItem("polly.retry.delay_ms").Should().BeOfType<double>()
            .Which.Should().BeGreaterThanOrEqualTo(delay.TotalMilliseconds);
    }

    [Test]
    public async Task CircuitBreaker_OpensAndEmitsActivity()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                MinimumThroughput = 2,
                FailureRatio = 1.0,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = TimeSpan.FromSeconds(1),
            })
            .Build();

        for (var i = 0; i < 2; i++)
        {
            try { await pipeline.ExecuteAsync(_ => throw new InvalidOperationException()); }
            catch { /* expected */ }
        }

        var openActivities = _activities.Where(a => a.OperationName == "polly.circuit_breaker.opened").ToList();
        openActivities.Should().HaveCount(1);
        openActivities[0].Status.Should().Be(ActivityStatusCode.Error);
        openActivities[0].GetTagItem("polly.circuit_breaker.break_duration_ms").Should().BeOfType<double>()
            .Which.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task Timeout_EmitsTimeoutActivity()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromMilliseconds(50),
            })
            .Build();

        try
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            });
        }
        catch (TimeoutRejectedException) { /* expected */ }

        var timeoutActivities = _activities.Where(a => a.OperationName == "polly.timeout").ToList();
        timeoutActivities.Should().HaveCount(1);
        timeoutActivities[0].Status.Should().Be(ActivityStatusCode.Error);
        timeoutActivities[0].GetTagItem("polly.timeout_ms").Should().BeOfType<double>()
            .Which.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task AttemptActivity_HasPipelineNameTags()
    {
        var pipeline = new ResiliencePipelineBuilder()
        {
            Name = "my-pipeline",
            InstanceName = "instance-1",
        }
            .AddPollyOpenTelemetry()
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 1, Delay = TimeSpan.Zero })
            .Build();

        await pipeline.ExecuteAsync(_ => ValueTask.CompletedTask);

        var attempt = _activities.First(a => a.OperationName == "polly.attempt");
        attempt.GetTagItem("polly.pipeline.name").Should().Be("my-pipeline");
        attempt.GetTagItem("polly.pipeline.instance_name").Should().Be("instance-1");
    }

    [Test]
    public void AddPollyOpenTelemetry_WhenTracingDisabled_EmitsNoActivities()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry(opt => opt.EnableTracing = false)
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 1, Delay = TimeSpan.Zero })
            .Build();

        pipeline.Execute(() => { });

        _activities.Should().BeEmpty();
    }

    [Test]
    public void ExistingTelemetryListener_IsPreservedWhenAddingOpenTelemetry()
    {
        var customCalls = new List<string>();
        var customListener = new CustomTelemetryListener(e => customCalls.Add(e));

        var builder = new ResiliencePipelineBuilder();
        builder.TelemetryListener = customListener;
        builder.AddPollyOpenTelemetry()
               .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 1, Delay = TimeSpan.Zero });

        var pipeline = builder.Build();
        pipeline.Execute(() => { });

        customCalls.Should().NotBeEmpty("existing listener should still receive events");
        _activities.Should().NotBeEmpty("OpenTelemetry listener should also receive events");
    }

    [Test]
    public async Task EnrichActivity_CallbackIsInvokedForEachActivity()
    {
        var enrichedActivities = new List<string>();

        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry(opt =>
            {
                opt.EnrichActivity = (activity, @event, _) =>
                    enrichedActivities.Add($"{activity.OperationName}:{@event.EventName}");
            })
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 1, Delay = TimeSpan.Zero })
            .Build();

        try { await pipeline.ExecuteAsync(_ => throw new InvalidOperationException()); }
        catch { /* expected */ }

        enrichedActivities.Should().Contain(s => s.StartsWith("polly.attempt:"));
        enrichedActivities.Should().Contain(s => s.StartsWith("polly.retry:"));
    }

    private sealed class CustomTelemetryListener(Action<string> onEvent) : Polly.Telemetry.TelemetryListener
    {
        public override void Write<TResult, TArgs>(in Polly.Telemetry.TelemetryEventArguments<TResult, TArgs> args)
            => onEvent(args.Event.EventName);
    }
}
