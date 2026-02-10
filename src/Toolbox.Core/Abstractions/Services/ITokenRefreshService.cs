// @file ITokenRefreshService.cs
// @brief Interface for automatic token refresh service
// @details Defines the contract for background token refresh operations

using Toolbox.Core.Options;

namespace Toolbox.Core.Abstractions.Services;

/// <summary>
/// Defines the contract for automatic token refresh operations.
/// </summary>
/// <remarks>
/// <para>
/// The token refresh service runs in the background and monitors registered
/// sessions for token expiration. When a session's tokens are about to expire
/// (based on the configured threshold), the service automatically refreshes them.
/// </para>
/// <para>
/// This service integrates with <see cref="ISsoSessionManager"/> to update
/// session tokens and with <see cref="ICredentialStore"/> to persist the
/// refreshed credentials.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The token refresh service is typically used internally by ISsoSessionManager
/// // and registered as a hosted service for background processing.
///
/// services.AddSsoServices(options =>
/// {
///     options.EnableAutoRefresh = true;
///     options.RefreshThreshold = 0.8; // Refresh at 80% of lifetime
///     options.RefreshCheckInterval = TimeSpan.FromMinutes(1);
/// });
/// </code>
/// </example>
public interface ITokenRefreshService : IAsyncDisposableService
{
    #region Registration

    /// <summary>
    /// Registers a session for automatic token refresh.
    /// </summary>
    /// <param name="session">The session to register.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="session"/> is null.
    /// </exception>
    /// <remarks>
    /// Sessions without a refresh token are accepted but will not be refreshed.
    /// </remarks>
    Task RegisterForRefreshAsync(
        SsoSession session,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a session from automatic token refresh.
    /// </summary>
    /// <param name="sessionId">The session ID to unregister.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><c>true</c> if the session was unregistered; otherwise, <c>false</c>.</returns>
    Task<bool> UnregisterFromRefreshAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the registration for a session (e.g., after token refresh).
    /// </summary>
    /// <param name="session">The updated session.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task UpdateRegistrationAsync(
        SsoSession session,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a session is registered for automatic refresh.
    /// </summary>
    /// <param name="sessionId">The session ID to check.</param>
    /// <returns><c>true</c> if the session is registered; otherwise, <c>false</c>.</returns>
    bool IsRegistered(string sessionId);

    #endregion

    #region Service Control

    /// <summary>
    /// Starts the background refresh service.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    /// <remarks>
    /// This method is typically called by the hosting infrastructure
    /// when the application starts.
    /// </remarks>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the background refresh service.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    /// <remarks>
    /// This method gracefully stops the refresh loop and completes
    /// any pending refresh operations.
    /// </remarks>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the service is running.
    /// </summary>
    bool IsRunning { get; }

    #endregion

    #region Manual Refresh

    /// <summary>
    /// Triggers an immediate refresh for a specific session.
    /// </summary>
    /// <param name="sessionId">The session ID to refresh.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The refreshed session, or null if refresh failed.</returns>
    Task<SsoSession?> RefreshNowAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers an immediate check of all registered sessions.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of sessions that were refreshed.</returns>
    Task<int> RefreshAllPendingAsync(
        CancellationToken cancellationToken = default);

    #endregion

    #region Statistics

    /// <summary>
    /// Gets the number of sessions registered for refresh.
    /// </summary>
    int RegisteredSessionCount { get; }

    /// <summary>
    /// Gets the number of successful refreshes since service start.
    /// </summary>
    long SuccessfulRefreshCount { get; }

    /// <summary>
    /// Gets the number of failed refreshes since service start.
    /// </summary>
    long FailedRefreshCount { get; }

    /// <summary>
    /// Gets the time of the last refresh check.
    /// </summary>
    DateTimeOffset? LastCheckTime { get; }

    /// <summary>
    /// Gets the time of the next scheduled refresh check.
    /// </summary>
    DateTimeOffset? NextCheckTime { get; }

    #endregion

    #region Events

    /// <summary>
    /// Raised when a token refresh is needed.
    /// </summary>
    event EventHandler<TokenRefreshNeededEventArgs>? RefreshNeeded;

    /// <summary>
    /// Raised when a token refresh completes successfully.
    /// </summary>
    event EventHandler<TokenRefreshCompletedEventArgs>? RefreshCompleted;

    /// <summary>
    /// Raised when a token refresh fails.
    /// </summary>
    event EventHandler<TokenRefreshFailedEventArgs>? RefreshFailed;

    #endregion
}
