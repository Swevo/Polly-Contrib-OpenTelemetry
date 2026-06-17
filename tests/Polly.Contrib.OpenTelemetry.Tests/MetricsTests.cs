// Copyright © 2025 Justin Bannister. MIT Licence.

using System.Diagnostics.Metrics;
using FluentAssertions;
using Polly;
using Polly.Contrib.OpenTelemetry;
using Polly.Retry;
using Polly.Timeout;

namespace Polly.Contrib.OpenTelemetry.Tests;

[TestFixture]
public class MetricsTests
{
    private MeterListener? _meterListener;
    private Dictionary<string, List<double>> _histogramMeasurements = [];
    private Dictionary<string, List<long>> _counterMeasurements = [];

    [SetUp]
    public void SetUp()
    {
        _histogramMeasurements = [];
        _counterMeasurements = [];

        _meterListener = new MeterListener();
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Polly")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
        {
            if (!_histogramMeasurements.TryGetValue(instrument.Name, out var list))
            {
                list = [];
                _histogramMeasurements[instrument.Name] = list;
            }

            list.Add(measurement);
        });

        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (!_counterMeasurements.TryGetValue(instrument.Name, out var list))
            {
                list = [];
                _counterMeasurements[instrument.Name] = list;
            }

            list.Add(measurement);
        });

        _meterListener.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _meterListener?.Dispose();
    }

    [Test]
    public async Task Retry_RecordsAttemptDurationForEachAttempt()
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

        _histogramMeasurements.Should().ContainKey("polly.attempt.duration");
        _histogramMeasurements["polly.attempt.duration"].Should().HaveCount(3,
            "3 attempts (2 failures + 1 success)");
        _histogramMeasurements["polly.attempt.duration"].Should().AllSatisfy(d => d.Should().BeGreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task Retry_RecordsRetryCount()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero,
                BackoffType = DelayBackoffType.Constant,
            })
            .Build();

        try { await pipeline.ExecuteAsync(_ => throw new InvalidOperationException()); }
        catch { /* expected */ }

        _counterMeasurements.Should().ContainKey("polly.retry.count");
        _counterMeasurements["polly.retry.count"].Sum().Should().Be(3,
            "3 retry decisions for MaxRetryAttempts = 3");
    }

    [Test]
    public async Task CircuitBreaker_RecordsOpenCount()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry()
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
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

        _counterMeasurements.Should().ContainKey("polly.circuit_breaker.open");
        _counterMeasurements["polly.circuit_breaker.open"].Sum().Should().Be(1);
    }

    [Test]
    public async Task Timeout_RecordsTimeoutCount()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry()
            .AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromMilliseconds(50) })
            .Build();

        try
        {
            await pipeline.ExecuteAsync(async ct => await Task.Delay(TimeSpan.FromSeconds(5), ct));
        }
        catch (TimeoutRejectedException) { /* expected */ }

        _counterMeasurements.Should().ContainKey("polly.timeout.count");
        _counterMeasurements["polly.timeout.count"].Sum().Should().Be(1);
    }

    [Test]
    public void AddPollyOpenTelemetry_WhenMetricsDisabled_EmitsNoMeasurements()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry(opt => opt.EnableMetrics = false)
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 1, Delay = TimeSpan.Zero })
            .Build();

        try { pipeline.Execute(() => throw new InvalidOperationException()); }
        catch { /* expected */ }

        _histogramMeasurements.Should().BeEmpty();
        _counterMeasurements.Should().BeEmpty();
    }

    [Test]
    public void SuccessfulExecution_RecordsAttemptDurationWithSuccessOutcome()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddPollyOpenTelemetry()
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 1, Delay = TimeSpan.Zero })
            .Build();

        pipeline.Execute(() => { });

        _histogramMeasurements.Should().ContainKey("polly.attempt.duration");
        _histogramMeasurements["polly.attempt.duration"].Should().HaveCount(1);
    }
}
