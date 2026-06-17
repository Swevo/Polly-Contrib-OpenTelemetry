// Copyright © 2025 Justin Bannister. MIT Licence.

using OpenTelemetry.Metrics;

namespace Polly.Contrib.OpenTelemetry;

/// <summary>
/// Extension methods on <see cref="MeterProviderBuilder"/> for Polly instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Polly meter so the OpenTelemetry SDK exports Polly metrics.
    /// </summary>
    /// <remarks>
    /// Call <c>builder.AddPollyOpenTelemetry()</c> on your <see cref="ResiliencePipelineBuilderBase"/>
    /// to activate metric emission.
    /// </remarks>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
    /// <returns>The same <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddPollyInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddMeter(PollyInstrumentation.MeterName);
    }
}
