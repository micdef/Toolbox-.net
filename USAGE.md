# Toolbox Usage Guide

This guide provides detailed instructions for using the Toolbox library.

## Table of Contents

1. [Getting Started](#getting-started)
2. [Creating Disposable Services](#creating-disposable-services)
3. [OpenTelemetry Integration](#opentelemetry-integration)
4. [Configuration Options](#configuration-options)
5. [Best Practices](#best-practices)

## Getting Started

### Basic Setup

Add Toolbox to your application:

```csharp
using Toolbox.Core.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Register Toolbox core services
builder.Services.AddToolboxCore();

var app = builder.Build();
```

### Configuration via appsettings.json

```json
{
  "Toolbox": {
    "EnableDetailedTelemetry": false,
    "ServicePrefix": "MyApp",
    "AsyncDisposalTimeout": "00:00:30"
  },
  "Toolbox:Telemetry": {
    "EnableTracing": true,
    "EnableMetrics": true,
    "EnableConsoleExport": false,
    "ServiceName": "MyApplication",
    "ServiceVersion": "1.0.0"
  }
}
```

```csharp
builder.Services.AddToolboxCore(builder.Configuration);
builder.Services.AddToolboxOpenTelemetry(builder.Configuration);
```

## Creating Disposable Services

### Synchronous Disposal

For services with synchronous cleanup:

```csharp
using Toolbox.Core.Base;

public class MyService : BaseDisposableService
{
    private readonly SomeResource _resource;

    public MyService(ILogger<MyService> logger)
        : base("MyService", logger)
    {
        _resource = new SomeResource();
    }

    public void DoWork()
    {
        ThrowIfDisposed();

        using var activity = StartActivity();
        var sw = Stopwatch.StartNew();

        // Do the work
        _resource.Process();

        RecordOperation("DoWork", sw.ElapsedMilliseconds);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _resource?.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

### Asynchronous Disposal

For services with async resources:

```csharp
using Toolbox.Core.Base;

public class MyAsyncService : BaseAsyncDisposableService
{
    private readonly Stream _stream;

    public MyAsyncService(ILogger<MyAsyncService> logger)
        : base("MyAsyncService", logger)
    {
        _stream = new FileStream("data.bin", FileMode.OpenOrCreate);
    }

    public async Task ProcessAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        using var activity = StartActivity();
        var sw = Stopwatch.StartNew();

        await _stream.WriteAsync(data, ct);

        RecordOperation("ProcessAsync", sw.ElapsedMilliseconds);
    }

    protected override async ValueTask DisposeAsyncCore(CancellationToken ct)
    {
        await _stream.DisposeAsync();
    }
}
```

## OpenTelemetry Integration

### Basic Tracing and Metrics

```csharp
builder.Services.AddToolboxOpenTelemetry(options =>
{
    options.EnableTracing = true;
    options.EnableMetrics = true;
    options.ServiceName = "MyApplication";
    options.ServiceVersion = "1.0.0";
});
```

### OTLP Export

```csharp
builder.Services.AddToolboxOpenTelemetry(options =>
{
    options.OtlpEndpoint = "http://localhost:4317";
});
```

### Custom Instrumentation

```csharp
using Toolbox.Core.Telemetry;

// Start a custom activity
using var activity = ToolboxActivitySource.StartActivity("CustomOperation");
activity?.SetTag("custom.tag", "value");

// Record metrics
ToolboxMeter.RecordOperation("MyService", "CustomOp", elapsedMs);
```

## Configuration Options

### ToolboxOptions

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableDetailedTelemetry` | bool | false | Enable detailed telemetry |
| `ServicePrefix` | string? | null | Prefix for service names |
| `AsyncDisposalTimeout` | TimeSpan | 30s | Timeout for async disposal |

### ToolboxTelemetryOptions

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableTracing` | bool | true | Enable distributed tracing |
| `EnableMetrics` | bool | true | Enable metrics collection |
| `EnableConsoleExport` | bool | false | Export to console |
| `OtlpEndpoint` | string? | null | OTLP collector endpoint |
| `ServiceName` | string | "Toolbox" | Service name for telemetry |
| `ServiceVersion` | string | "1.0.0" | Service version |

## Best Practices

### 1. Always Check Disposal State

```csharp
public void DoWork()
{
    ThrowIfDisposed();  // Call this first
    // ... rest of method
}
```

### 2. Use Activity Scopes for Tracing

```csharp
public async Task DoWorkAsync()
{
    using var activity = StartActivity();
    // Activity automatically ends when scope exits
}
```

### 3. Record Metrics for Performance

```csharp
var sw = Stopwatch.StartNew();
// ... operation
RecordOperation("OperationName", sw.ElapsedMilliseconds);
```

### 4. Handle Cancellation in Async Disposal

```csharp
protected override async ValueTask DisposeAsyncCore(CancellationToken ct)
{
    try
    {
        await _resource.CloseAsync(ct);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        // Force close if cancelled
        _resource.ForceClose();
    }
}
```

### 5. Register Services with Proper Lifetime

```csharp
// Scoped for per-request services
services.AddScoped<IMyService, MyService>();

// Singleton for shared services
services.AddSingleton<ISharedService, SharedService>();
```
