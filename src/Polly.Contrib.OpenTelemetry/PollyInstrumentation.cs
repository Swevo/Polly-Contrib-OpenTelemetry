// Copyright © 2025 Justin Bannister. MIT Licence.

namespace Polly.Contrib.OpenTelemetry;

internal static class PollyInstrumentation
{
    internal const string ActivitySourceName = "Polly";
    internal const string ActivitySourceVersion = "1.0.0";
    internal const string MeterName = "Polly";
    internal const string MeterVersion = "1.0.0";
}

internal static class MetricNames
{
    internal const string AttemptDuration = "polly.attempt.duration";
    internal const string RetryCount = "polly.retry.count";
    internal const string CircuitBreakerOpenCount = "polly.circuit_breaker.open";
    internal const string TimeoutCount = "polly.timeout.count";
}

internal static class ActivityNames
{
    internal const string Attempt = "polly.attempt";
    internal const string Retry = "polly.retry";
    internal const string CircuitBreakerOpened = "polly.circuit_breaker.opened";
    internal const string CircuitBreakerClosed = "polly.circuit_breaker.closed";
    internal const string CircuitBreakerHalfOpened = "polly.circuit_breaker.half_opened";
    internal const string Timeout = "polly.timeout";
}

internal static class Tags
{
    internal const string PipelineName = "polly.pipeline.name";
    internal const string PipelineInstanceName = "polly.pipeline.instance_name";
    internal const string StrategyName = "polly.strategy.name";
    internal const string AttemptNumber = "polly.attempt.number";
    internal const string AttemptHandled = "polly.attempt.handled";
    internal const string RetryDelayMs = "polly.retry.delay_ms";
    internal const string CircuitBreakerBreakDurationMs = "polly.circuit_breaker.break_duration_ms";
    internal const string CircuitBreakerIsManual = "polly.circuit_breaker.is_manual";
    internal const string TimeoutMs = "polly.timeout_ms";
    internal const string Outcome = "polly.outcome";
    internal const string ErrorType = "error.type";
}
