// Copyright © 2025 Justin Bannister. MIT Licence.

namespace Polly.Contrib.OpenTelemetry;

/// <summary>
/// Extension methods on <see cref="ResiliencePipelineBuilderBase"/> for adding
/// OpenTelemetry instrumentation to Polly resilience pipelines.
/// </summary>
public static class ResiliencePipelineBuilderExtensions
{
    /// <summary>
    /// Adds OpenTelemetry instrumentation to the resilience pipeline.
    /// Emits <see cref="System.Diagnostics.Activity"/> spans and metrics for
    /// retry, circuit breaker, timeout, and rate limiter events.
    /// </summary>
    /// <remarks>
    /// If a <see cref="Polly.Telemetry.TelemetryListener"/> is already registered on the builder
    /// (e.g. via <c>ConfigureTelemetry(ILoggerFactory)</c>), the OpenTelemetry listener is composed
    /// on top of it so both continue to receive events.
    ///
    /// Register the Polly <see cref="System.Diagnostics.ActivitySource"/> and
    /// <see cref="System.Diagnostics.Metrics.Meter"/> with the OpenTelemetry SDK:
    /// <code>
    /// services.AddOpenTelemetry()
    ///     .WithTracing(t => t.AddPollyInstrumentation())
    ///     .WithMetrics(m => m.AddPollyInstrumentation());
    /// </code>
    /// </remarks>
    /// <typeparam name="TBuilder">Concrete builder type.</typeparam>
    /// <param name="builder">The pipeline builder.</param>
    /// <param name="configure">Optional callback to configure instrumentation options.</param>
    /// <returns>The same builder for chaining.</returns>
    public static TBuilder AddPollyOpenTelemetry<TBuilder>(
        this TBuilder builder,
        Action<PollyInstrumentationOptions>? configure = null)
        where TBuilder : ResiliencePipelineBuilderBase
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new PollyInstrumentationOptions();
        configure?.Invoke(options);

        var newListener = new PollyTelemetryListener(options);

        builder.TelemetryListener = builder.TelemetryListener is { } existing
            ? new CompositeTelemetryListener(existing, newListener)
            : newListener;

        return builder;
    }
}
