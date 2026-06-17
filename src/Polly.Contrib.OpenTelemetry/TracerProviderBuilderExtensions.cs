// Copyright © 2025 Justin Bannister. MIT Licence.

using OpenTelemetry.Trace;

namespace Polly.Contrib.OpenTelemetry;

/// <summary>
/// Extension methods on <see cref="TracerProviderBuilder"/> for Polly instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Registers the Polly <see cref="System.Diagnostics.ActivitySource"/> so the OpenTelemetry SDK
    /// exports Polly trace spans.
    /// </summary>
    /// <remarks>
    /// Call <c>builder.AddPollyOpenTelemetry()</c> on your <see cref="ResiliencePipelineBuilderBase"/>
    /// to activate span emission.
    /// </remarks>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> to configure.</param>
    /// <returns>The same <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddPollyInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(PollyInstrumentation.ActivitySourceName);
    }
}
