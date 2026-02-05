// @file BaseDisposableService.cs
// @brief Abstract base class for synchronously disposable services
// @details Implements thread-safe dispose pattern with telemetry integration
// @note Derive from this class for services that only need synchronous disposal

using Toolbox.Core.Abstractions;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Base;

/// <summary>
/// Abstract base class providing a thread-safe implementation of the dispose pattern
/// with integrated telemetry support.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IDisposableService"/> and <see cref="IInstrumentedService"/>,
/// providing a consistent foundation for building disposable services.
/// </para>
/// <para>
/// Derived classes should override <see cref="Dispose(bool)"/> to release their resources.
/// The base implementation handles thread-safety and telemetry automatically.
/// </para>
/// </remarks>
/// <seealso cref="IDisposableService"/>
/// <seealso cref="IInstrumentedService"/>
public abstract class BaseDisposableService : IDisposableService, IInstrumentedService
{
    // Flag indicating disposal state (0 = not disposed, 1 = disposed)
    private int _disposed;

    // The logger instance for this service
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseDisposableService"/> class.
    /// </summary>
    /// <param name="serviceName">The name identifying this service instance.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="serviceName"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    protected BaseDisposableService(string serviceName, ILogger logger)
    {
        ServiceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ToolboxMeter.IncrementActiveInstances(ServiceName);
        _logger.LogDebug("Service {ServiceName} created", ServiceName);
    }

    /// <inheritdoc />
    public string ServiceName { get; }

    /// <inheritdoc />
    public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    /// <inheritdoc />
    public ActivitySource ActivitySource => ToolboxActivitySource.Instance;

    /// <inheritdoc />
    public Meter Meter => ToolboxMeter.Instance;

    /// <summary>
    /// Releases all resources used by this service.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and can be called multiple times.
    /// Only the first call will release resources.
    /// </remarks>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by this service and optionally releases managed resources.
    /// </summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources;
    /// <c>false</c> to release only unmanaged resources.
    /// </param>
    /// <remarks>
    /// <para>
    /// Override this method in derived classes to release resources.
    /// Always call the base implementation after releasing your resources.
    /// </para>
    /// <para>
    /// This method is thread-safe. The <paramref name="disposing"/> parameter
    /// will only be <c>true</c> for the first caller when called concurrently.
    /// </para>
    /// </remarks>
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (disposing)
        {
            using var activity = ActivitySource.StartActivity($"{ServiceName}.Dispose");
            activity?.SetTag(TelemetryConstants.Attributes.ServiceName, ServiceName);

            _logger.LogDebug("Service {ServiceName} disposing", ServiceName);
            ToolboxMeter.RecordDisposal(ServiceName);
        }
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if this service has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when <see cref="IsDisposed"/> is <c>true</c>.
    /// </exception>
    /// <remarks>
    /// Call this method at the beginning of public methods to ensure the service is still usable.
    /// </remarks>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }

    /// <summary>
    /// Records an operation metric with the specified name and duration.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="durationMs">The duration in milliseconds.</param>
    protected void RecordOperation(string operationName, double durationMs)
    {
        ToolboxMeter.RecordOperation(ServiceName, operationName, durationMs);
    }

    /// <summary>
    /// Starts a new activity for the specified operation.
    /// </summary>
    /// <param name="operationName">The name of the operation. Defaults to the calling method name.</param>
    /// <returns>
    /// The started <see cref="Activity"/> if tracing is enabled; otherwise, <c>null</c>.
    /// </returns>
    protected Activity? StartActivity([CallerMemberName] string operationName = "")
    {
        var activity = ActivitySource.StartActivity($"{ServiceName}.{operationName}");
        activity?.SetTag(TelemetryConstants.Attributes.ServiceName, ServiceName);
        activity?.SetTag(TelemetryConstants.Attributes.OperationName, operationName);
        return activity;
    }
}
