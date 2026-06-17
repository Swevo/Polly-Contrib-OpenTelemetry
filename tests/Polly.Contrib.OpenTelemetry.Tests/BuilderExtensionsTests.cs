// Copyright © 2025 Justin Bannister. MIT Licence.

using FluentAssertions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Polly;
using Polly.Contrib.OpenTelemetry;

namespace Polly.Contrib.OpenTelemetry.Tests;

[TestFixture]
public class BuilderExtensionsTests
{
    [Test]
    public void AddPollyOpenTelemetry_NullBuilder_Throws()
    {
        ResiliencePipelineBuilder builder = null!;
        var act = () => builder.AddPollyOpenTelemetry();
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void AddPollyOpenTelemetry_SetsListenerOnBuilder()
    {
        var builder = new ResiliencePipelineBuilder();
        builder.TelemetryListener.Should().BeNull();

        builder.AddPollyOpenTelemetry();

        builder.TelemetryListener.Should().NotBeNull();
    }

    [Test]
    public void AddPollyOpenTelemetry_WhenListenerAlreadySet_CreatesComposite()
    {
        var existing = new FakeTelemetryListener();
        var builder = new ResiliencePipelineBuilder { TelemetryListener = existing };

        builder.AddPollyOpenTelemetry();

        builder.TelemetryListener.Should().BeOfType<CompositeTelemetryListener>(
            "existing + OpenTelemetry listener should be composed");
    }

    [Test]
    public void AddPollyOpenTelemetry_ReturnsBuilderForChaining()
    {
        var builder = new ResiliencePipelineBuilder();
        var returned = builder.AddPollyOpenTelemetry();
        returned.Should().BeSameAs(builder);
    }

    [Test]
    public void AddPollyOpenTelemetry_GenericBuilder_Works()
    {
        var builder = new ResiliencePipelineBuilder<string>();
        builder.AddPollyOpenTelemetry();
        builder.TelemetryListener.Should().NotBeNull();
    }

    [Test]
    public void TracerProviderBuilder_AddPollyInstrumentation_NullBuilder_Throws()
    {
        TracerProviderBuilder builder = null!;
        var act = () => builder.AddPollyInstrumentation();
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void MeterProviderBuilder_AddPollyInstrumentation_NullBuilder_Throws()
    {
        MeterProviderBuilder builder = null!;
        var act = () => builder.AddPollyInstrumentation();
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Configure_InstrumentationOptions_AreApplied()
    {
        var builder = new ResiliencePipelineBuilder();

        builder.AddPollyOpenTelemetry(opt =>
        {
            opt.EnableTracing = false;
            opt.EnableMetrics = false;
        });

        // No assertion on internals; just verify it doesn't throw
        builder.Build().Should().NotBeNull();
    }

    private sealed class FakeTelemetryListener : Polly.Telemetry.TelemetryListener
    {
        public override void Write<TResult, TArgs>(in Polly.Telemetry.TelemetryEventArguments<TResult, TArgs> args) { }
    }
}
