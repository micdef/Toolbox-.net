// @file BaseAsyncDisposableService.cs
// @brief Abstract base class for asynchronously disposable services
// @details Extends BaseDisposableService with async disposal and cancellation support
// @note Derive from this class for services managing async resources (streams, connections, etc.)

using Toolbox.Core.Abstractions;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Base;

/// <summary>
/// Abstract base class providing asynchronous disposal with cancellation support
/// and integrated telemetry.
/// </summary>
/// <remarks>
/// <para>
/// This class extends <see cref="BaseDisposableService"/> to implement
/// <see cref="IAsyncDisposableService"/>, enabling graceful async cleanup
/// with timeout support through cancellation tokens.
/// </para>
/// <para>
/// Derived classes should override <see cref="DisposeAsyncCore(CancellationToken)"/>
/// to release their async resources.
/// </para>
/// </remarks>
/// <seealso cref="IAsyncDisposableService"/>
/// <seealso cref="BaseDisposableService"/>
public abstract class BaseAsyncDisposableService : BaseDisposableService, IAsyncDisposableService
{
    // Flag indicating async disposal state (0 = not disposed, 1 = disposing/disposed)
    private int _asyncDisposed;

    // The logger instance for this service
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseAsyncDisposableService"/> class.
    /// </summary>
    /// <param name="serviceName">The name identifying this service instance.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="serviceName"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    protected BaseAsyncDisposableService(string serviceName, ILogger logger)
        : base(serviceName, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This implementation calls <see cref="DisposeAsync(CancellationToken)"/>
    /// with <see cref="CancellationToken.None"/>.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        return DisposeAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _asyncDisposed, 1) == 1)
        {
            return;
        }

        using var activity = ActivitySource.StartActivity($"{ServiceName}.DisposeAsync");
        activity?.SetTag(TelemetryConstants.Attributes.ServiceName, ServiceName);

        _logger.LogDebug("Service {ServiceName} disposing asynchronously", ServiceName);

        try
        {
            await DisposeAsyncCore(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Async disposal of {ServiceName} was cancelled", ServiceName);
            activity?.SetStatus(ActivityStatusCode.Error, "Disposal cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during async disposal of {ServiceName}", ServiceName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            Dispose(disposing: false);
            GC.SuppressFinalize(this);

            ToolboxMeter.RecordDisposal(ServiceName, "async");
        }
    }

    /// <summary>
    /// Performs application-defined async cleanup operations.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> that can be used to cancel the disposal operation.
    /// </param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// Override this method to release async resources such as streams, database connections,
    /// or other async-disposable objects.
    /// </para>
    /// <para>
    /// If the cancellation token is triggered, the implementation should attempt to
    /// release critical resources immediately and may throw <see cref="OperationCanceledException"/>.
    /// </para>
    /// </remarks>
    protected virtual ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This override ensures that synchronous disposal also triggers async resource cleanup
    /// when the service hasn't been disposed asynchronously yet.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing && Volatile.Read(ref _asyncDisposed) == 0)
        {
            // If synchronous Dispose is called before DisposeAsync,
            // run the async disposal synchronously
            try
            {
                DisposeAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during synchronous disposal of async service {ServiceName}", ServiceName);
            }
        }

        base.Dispose(disposing);
    }
}
