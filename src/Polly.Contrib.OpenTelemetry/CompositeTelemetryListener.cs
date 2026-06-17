// Copyright © 2025 Justin Bannister. MIT Licence.

using Polly.Telemetry;

namespace Polly.Contrib.OpenTelemetry;

/// <summary>
/// Composes multiple <see cref="TelemetryListener"/> instances into a single listener,
/// preserving any listener already registered on the pipeline builder.
/// </summary>
internal sealed class CompositeTelemetryListener : TelemetryListener
{
    private readonly TelemetryListener[] _listeners;

    internal CompositeTelemetryListener(params TelemetryListener[] listeners)
    {
        _listeners = listeners;
    }

    /// <inheritdoc/>
    public override void Write<TResult, TArgs>(in TelemetryEventArguments<TResult, TArgs> args)
    {
        foreach (var listener in _listeners)
        {
            listener.Write(in args);
        }
    }
}
