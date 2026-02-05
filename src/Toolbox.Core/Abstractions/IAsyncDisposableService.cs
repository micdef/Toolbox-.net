// @file IAsyncDisposableService.cs
// @brief Interface for asynchronously disposable services
// @details Extends IDisposableService with async disposal capabilities and cancellation support
// @note Prefer this interface for services that manage async resources (streams, connections, etc.)

namespace Toolbox.Core.Abstractions;

/// <summary>
/// Interface for services requiring asynchronous disposal with cancellation support.
/// </summary>
/// <remarks>
/// This interface combines <see cref="IDisposableService"/> and <see cref="IAsyncDisposable"/>
/// with an additional method supporting cancellation tokens for graceful shutdown scenarios.
/// </remarks>
/// <seealso cref="IDisposableService"/>
public interface IAsyncDisposableService : IDisposableService, IAsyncDisposable
{
    /// <summary>
    /// Asynchronously releases resources with cancellation support.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> that can be used to cancel the disposal operation.
    /// </param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous disposal operation.</returns>
    /// <remarks>
    /// <para>
    /// This method allows for graceful shutdown with timeout support. If the cancellation token
    /// is triggered, the implementation should attempt to release critical resources immediately.
    /// </para>
    /// <para>
    /// The standard <see cref="IAsyncDisposable.DisposeAsync"/> method should call this method
    /// with <see cref="CancellationToken.None"/>.
    /// </para>
    /// </remarks>
    ValueTask DisposeAsync(CancellationToken cancellationToken);
}
