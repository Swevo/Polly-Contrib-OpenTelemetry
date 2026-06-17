// Copyright © 2025 Justin Bannister. MIT Licence.

using System.Diagnostics;
using Polly.Telemetry;

namespace Polly.Contrib.OpenTelemetry;

/// <summary>
/// Options for configuring Polly OpenTelemetry instrumentation.
/// </summary>
public sealed class PollyInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to emit tracing <see cref="Activity"/> spans.
    /// Default is <c>true</c>.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to emit metrics.
    /// Default is <c>true</c>.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional callback to enrich emitted activities with additional tags.
    /// </summary>
    public Action<Activity, ResilienceEvent, ResilienceTelemetrySource>? EnrichActivity { get; set; }
}
