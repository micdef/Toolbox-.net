// Sample application demonstrating Toolbox usage
// Shows how to configure and use Toolbox services with DI and OpenTelemetry

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Toolbox.Core.Base;
using Toolbox.Core.Extensions;

// Create and configure the host
var builder = Host.CreateApplicationBuilder(args);

// Add Toolbox core services
builder.Services.AddToolboxCore(options =>
{
    options.EnableDetailedTelemetry = true;
    options.ServicePrefix = "Sample";
});

// Add OpenTelemetry with console export for demonstration
builder.Services.AddToolboxOpenTelemetry(options =>
{
    options.EnableTracing = true;
    options.EnableMetrics = true;
    options.EnableConsoleExport = true;
    options.ServiceName = "Toolbox.Sample";
    options.ServiceVersion = "1.0.0";
});

// Register our sample service
builder.Services.AddScoped<ISampleService, SampleService>();

var app = builder.Build();

// Demonstrate service usage
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Starting Toolbox Sample Application");

    var service = scope.ServiceProvider.GetRequiredService<ISampleService>();

    // Perform some operations
    await service.DoWorkAsync("Hello, Toolbox!");

    logger.LogInformation("Sample application completed successfully");
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();

/// <summary>
/// Sample service interface demonstrating Toolbox patterns.
/// </summary>
public interface ISampleService
{
    /// <summary>
    /// Performs sample work asynchronously.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task DoWorkAsync(string message, CancellationToken ct = default);
}

/// <summary>
/// Sample service implementation using BaseAsyncDisposableService.
/// </summary>
public sealed class SampleService : BaseAsyncDisposableService, ISampleService
{
    private readonly ILogger<SampleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SampleService(ILogger<SampleService> logger)
        : base("SampleService", logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DoWorkAsync(string message, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation("Processing message: {Message}", message);

        // Simulate some async work
        await Task.Delay(100, ct);

        _logger.LogInformation("Message processed successfully");

        RecordOperation("DoWorkAsync", sw.ElapsedMilliseconds);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SampleService is being disposed");
        await Task.CompletedTask;
    }
}
