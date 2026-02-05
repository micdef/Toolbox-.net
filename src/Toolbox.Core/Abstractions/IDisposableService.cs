// @file IDisposableService.cs
// @brief Base interface for disposable services
// @details Defines the contract for services that require deterministic cleanup
// @note All disposable services should implement this interface for consistent lifecycle management

namespace Toolbox.Core.Abstractions;

/// <summary>
/// Base interface for disposable services providing lifecycle management.
/// </summary>
/// <remarks>
/// This interface extends <see cref="IDisposable"/> with additional properties
/// for service identification and disposal state tracking.
/// </remarks>
/// <seealso cref="IAsyncDisposableService"/>
public interface IDisposableService : IDisposable
{
    /// <summary>
    /// Gets the unique name identifying this service instance.
    /// </summary>
    /// <value>A human-readable name for logging and diagnostics.</value>
    string ServiceName { get; }

    /// <summary>
    /// Gets a value indicating whether this service has been disposed.
    /// </summary>
    /// <value><c>true</c> if the service has been disposed; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// This property should be checked before performing any operation on the service.
    /// Operations on disposed services should throw <see cref="ObjectDisposedException"/>.
    /// </remarks>
    bool IsDisposed { get; }
}
