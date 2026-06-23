# PollyOpenTelemetry

<img src="icon.png" width="100" align="right" />

[![NuGet](https://img.shields.io/nuget/v/PollyOpenTelemetry)](https://www.nuget.org/packages/PollyOpenTelemetry)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyOpenTelemetry.svg)](https://www.nuget.org/packages/PollyOpenTelemetry)

OpenTelemetry instrumentation for [Polly v8](https://github.com/App-vNext/Polly) resilience pipelines.

Emits **distributed trace spans** (`Activity`) and **metrics** for retry, circuit breaker, and timeout strategies — giving you full observability into your resilience layer.

> Polly ships built-in logging and basic metrics via `ConfigureTelemetry(ILoggerFactory)`.  
> This package adds the missing piece: **OpenTelemetry-native traces and enriched metrics**.

---

## Installation

```
dotnet add package PollyOpenTelemetry
```

---

## Quick Start

### 1. Register with the OpenTelemetry SDK

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddPollyInstrumentation()          // register the "Polly" ActivitySource
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddPollyInstrumentation()          // register the "Polly" Meter
        .AddOtlpExporter());
```

### 2. Add to your resilience pipeline

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddPollyOpenTelemetry()                // ← one call enables both traces + metrics
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        MinimumThroughput = 10,
    })
    .AddTimeout(TimeSpan.FromSeconds(30))
    .Build();
```

### With Dependency Injection

```csharp
services.AddResiliencePipeline("my-http-client", builder =>
{
    builder
        .AddPollyOpenTelemetry()
        .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3 })
        .AddTimeout(TimeSpan.FromSeconds(30));
});
```

---

## Emitted Telemetry

### Activities (Traces)

| Activity name                       | When emitted                              |
|-------------------------------------|-------------------------------------------|
| `polly.attempt`                     | Each individual execution attempt         |
| `polly.retry`                       | Each retry decision                       |
| `polly.circuit_breaker.opened`      | Circuit breaker transitions to open       |
| `polly.circuit_breaker.closed`      | Circuit breaker transitions to closed     |
| `polly.circuit_breaker.half_opened` | Circuit breaker transitions to half-open  |
| `polly.timeout`                     | Operation cancelled due to timeout        |

### Common Activity Tags

| Tag                           | Value                              |
|-------------------------------|------------------------------------|
| `polly.pipeline.name`         | `ResiliencePipelineBuilder.Name`   |
| `polly.pipeline.instance_name`| `ResiliencePipelineBuilder.InstanceName` |
| `polly.strategy.name`         | Name of the individual strategy    |
| `polly.outcome`               | `success`, `failure`, or `unknown` |
| `error.type`                  | Exception type (on failure)        |

### Additional Tags per Activity

**`polly.attempt`**: `polly.attempt.number`, `polly.attempt.handled`  
**`polly.retry`**: `polly.attempt.number`, `polly.retry.delay_ms`  
**`polly.circuit_breaker.opened`**: `polly.circuit_breaker.break_duration_ms`, `polly.circuit_breaker.is_manual`  
**`polly.timeout`**: `polly.timeout_ms`

### Metrics

| Metric name                     | Type      | Unit        | Description                                       |
|---------------------------------|-----------|-------------|---------------------------------------------------|
| `polly.attempt.duration`        | Histogram | ms          | Duration of each individual execution attempt     |
| `polly.retry.count`             | Counter   | `{attempt}` | Number of retry decisions made                    |
| `polly.circuit_breaker.open`    | Counter   | `{open}`    | Number of times circuit breaker opened            |
| `polly.timeout.count`           | Counter   | `{timeout}` | Number of operations cancelled due to timeout     |

---

## Options

```csharp
builder.AddPollyOpenTelemetry(options =>
{
    options.EnableTracing = true;   // default: true
    options.EnableMetrics = true;   // default: true

    // Enrich activities with custom tags
    options.EnrichActivity = (activity, resilienceEvent, source) =>
    {
        activity.SetTag("my.custom.tag", "value");
    };
});
```

---

## Composing with Polly's Built-in Logging

`AddPollyOpenTelemetry()` is safe to combine with Polly's built-in `ConfigureTelemetry(ILoggerFactory)` — both listeners are composed automatically:

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .ConfigureTelemetry(loggerFactory)    // Polly structured logging + basic metrics
    .AddPollyOpenTelemetry()              // OpenTelemetry traces + enriched metrics
    .AddRetry(...)
    .Build();
```

---

## Sample Trace Output

```
▶ polly.attempt   [polly.pipeline.name=payment-api] [polly.attempt.number=0] [polly.outcome=failure]
  ✗ error.type=System.Net.Http.HttpRequestException
▶ polly.retry     [polly.retry.delay_ms=200] [polly.attempt.number=0]
▶ polly.attempt   [polly.pipeline.name=payment-api] [polly.attempt.number=1] [polly.outcome=success]
```

---

## Requirements

- .NET 8.0+
- Polly 8.x
- OpenTelemetry 1.x

---

## Publishing

This package is published to NuGet.org via **GitHub Actions using [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package#trusted-publishing)** — no API key secret is stored in the repository.

### Setup (one-time)

1. On [nuget.org](https://www.nuget.org), go to your package → **Manage** → **Trusted Publishers** → **Add a publisher**  
2. Choose **GitHub Actions** and enter:  
   - **Owner**: `Sweevo`  
   - **Repository**: `Polly-Contrib-OpenTelemetry`  
   - **Workflow**: `build.yml`  
3. Push a `v*` tag to trigger the publish workflow:
   ```bash
   git tag v1.0.0
   git push --tags
   ```

The workflow requests a short-lived OIDC token from GitHub (audience `api.nuget.org`) and uses it as the push credential — no `NUGET_API_KEY` secret required.

---

## Support

If PollyOpenTelemetry improves your observability, consider supporting the project:

[![Sponsor](https://img.shields.io/badge/Sponsor-%E2%9D%A4-pink?logo=github)](https://github.com/sponsors/Swevo)

> 💼 **Need .NET observability or resilience help?** Visit [solidqualitysolutions.com](https://solidqualitysolutions.com/) for consulting and architecture services.

## Related packages

| Package | Description |
|---|---|
| [PollyChaos](https://www.nuget.org/packages/PollyChaos) | Chaos engineering — inject faults & latency (Simmy for v8) |
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | Polly v8 pipelines for MediatR request handlers |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | Polly v8 resilience for EF Core queries and SaveChanges |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | Backoff delay strategies |
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI — retry on 429, Retry-After, circuit breaker |
| [PollyRedis](https://www.nuget.org/packages/PollyRedis) | [![Downloads](https://img.shields.io/nuget/dt/PollyRedis.svg)](https://www.nuget.org/packages/PollyRedis) | Polly v8 resilience for StackExchange.Redis — retry, circuit breaker, timeout |
| [PollySignalR](https://www.nuget.org/packages/PollySignalR) | [![Downloads](https://img.shields.io/nuget/dt/PollySignalR.svg)](https://www.nuget.org/packages/PollySignalR) | Polly v8 exponential back-off reconnect policy for SignalR HubConnection |
| [PollyGrpc](https://www.nuget.org/packages/PollyGrpc) | Polly v8 resilience (retry, CB, timeout) for gRPC .NET clients via Interceptor |
| [PollyKafka](https://www.nuget.org/packages/PollyKafka) | Polly v8 resilience (retry, CB, timeout) for Confluent.Kafka producers and consumers |
| [PollyAzureServiceBus](https://www.nuget.org/packages/PollyAzureServiceBus) | Polly v8 resilience (retry, CB, timeout) for Azure Service Bus senders and receivers |
| [PollyCaching](https://www.nuget.org/packages/PollyCaching) | Caching resilience strategy |
| [PollyBulkhead](https://www.nuget.org/packages/PollyBulkhead) | Bulkhead isolation |
| [PollyRateLimiter](https://www.nuget.org/packages/PollyRateLimiter) | Rate limiting strategies |

## License

MIT — Copyright © 2025 Justin Bannister
