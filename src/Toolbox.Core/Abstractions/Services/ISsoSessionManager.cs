// @file ISsoSessionManager.cs
// @brief Interface for SSO session management
// @details Defines the contract for creating, validating, refreshing, and revoking SSO sessions

using Toolbox.Core.Options;

namespace Toolbox.Core.Abstractions.Services;

/// <summary>
/// Defines the contract for Single Sign-On (SSO) session management.
/// </summary>
/// <remarks>
/// <para>
/// The session manager is responsible for the complete lifecycle of SSO sessions:
/// </para>
/// <list type="bullet">
///   <item><description>Creating sessions from authentication results</description></item>
///   <item><description>Validating existing sessions</description></item>
///   <item><description>Refreshing session tokens</description></item>
///   <item><description>Revoking sessions (logout)</description></item>
///   <item><description>Managing session limits per user</description></item>
/// </list>
/// <para>
/// The session manager integrates with <see cref="ICredentialStore"/> for persistence
/// and <see cref="ITokenRefreshService"/> for automatic token refresh.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class AuthController
/// {
///     private readonly ISsoSessionManager _sessionManager;
///     private readonly ILdapService _ldapService;
///
///     public async Task&lt;SsoSession&gt; LoginAsync(string username, string password)
///     {
///         var authResult = await _ldapService.AuthenticateAsync(new LdapAuthenticationOptions
///         {
///             Mode = LdapAuthenticationMode.Simple,
///             Username = username,
///             Password = password
///         });
///
///         if (!authResult.IsAuthenticated)
///             throw new AuthenticationException(authResult.ErrorMessage);
///
///         return await _sessionManager.CreateSessionAsync(authResult, _ldapService);
///     }
///
///     public async Task LogoutAsync(string sessionId)
///     {
///         await _sessionManager.RevokeSessionAsync(sessionId);
///     }
/// }
/// </code>
/// </example>
public interface ISsoSessionManager : IAsyncDisposableService
{
    #region Session Creation

    /// <summary>
    /// Creates a new SSO session from an LDAP authentication result.
    /// </summary>
    /// <param name="authResult">The successful authentication result.</param>
    /// <param name="ldapService">The LDAP service that performed the authentication.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The newly created SSO session.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="authResult"/> or <paramref name="ldapService"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the authentication result indicates failure or when
    /// the user has reached the maximum number of sessions.
    /// </exception>
    Task<SsoSession> CreateSessionAsync(
        LdapAuthenticationResult authResult,
        ILdapService ldapService,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new SSO session from an LDAP authentication result with device binding.
    /// </summary>
    /// <param name="authResult">The successful authentication result.</param>
    /// <param name="ldapService">The LDAP service that performed the authentication.</param>
    /// <param name="deviceId">The device identifier to bind the session to.</param>
    /// <param name="ipAddress">The IP address to bind the session to.</param>
    /// <param name="userAgent">The user agent string.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The newly created SSO session.</returns>
    Task<SsoSession> CreateSessionAsync(
        LdapAuthenticationResult authResult,
        ILdapService ldapService,
        string? deviceId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    #endregion

    #region Session Validation

    /// <summary>
    /// Validates an existing SSO session.
    /// </summary>
    /// <param name="sessionId">The session ID to validate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The validation result including the session if valid.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sessionId"/> is null or empty.
    /// </exception>
    Task<SsoSessionValidationResult> ValidateSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an existing SSO session with device/IP checking.
    /// </summary>
    /// <param name="sessionId">The session ID to validate.</param>
    /// <param name="deviceId">The current device identifier.</param>
    /// <param name="ipAddress">The current IP address.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The validation result including the session if valid.</returns>
    Task<SsoSessionValidationResult> ValidateSessionAsync(
        string sessionId,
        string? deviceId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    #endregion

    #region Session Refresh

    /// <summary>
    /// Refreshes the tokens for an existing session.
    /// </summary>
    /// <param name="sessionId">The session ID to refresh.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The refreshed session.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sessionId"/> is null or empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the session cannot be refreshed (expired, revoked, or no refresh token).
    /// </exception>
    Task<SsoSession> RefreshSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last activity time for sliding expiration.
    /// </summary>
    /// <param name="sessionId">The session ID to touch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task TouchSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Session Revocation

    /// <summary>
    /// Revokes (logs out) a session.
    /// </summary>
    /// <param name="sessionId">The session ID to revoke.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sessionId"/> is null or empty.
    /// </exception>
    Task RevokeSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all sessions for a specific user.
    /// </summary>
    /// <param name="userId">The user ID whose sessions should be revoked.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of sessions revoked.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="userId"/> is null or empty.
    /// </exception>
    Task<int> RevokeAllUserSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all sessions for a user except the specified session.
    /// </summary>
    /// <param name="userId">The user ID whose sessions should be revoked.</param>
    /// <param name="exceptSessionId">The session ID to keep active.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of sessions revoked.</returns>
    Task<int> RevokeOtherSessionsAsync(
        string userId,
        string exceptSessionId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Session Queries

    /// <summary>
    /// Gets a session by its ID.
    /// </summary>
    /// <param name="sessionId">The session ID to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The session if found, or null.</returns>
    Task<SsoSession?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active sessions for a user.
    /// </summary>
    /// <param name="userId">The user ID to query.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of active sessions for the user.</returns>
    Task<IReadOnlyList<SsoSession>> GetUserSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active sessions for a user.
    /// </summary>
    /// <param name="userId">The user ID to query.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of active sessions.</returns>
    Task<int> GetUserSessionCountAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of active sessions.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The total number of active sessions.</returns>
    Task<int> GetActiveSessionCountAsync(
        CancellationToken cancellationToken = default);

    #endregion

    #region Events

    /// <summary>
    /// Raised when a new session is created.
    /// </summary>
    event EventHandler<SsoSessionCreatedEventArgs>? SessionCreated;

    /// <summary>
    /// Raised when a session is about to expire.
    /// </summary>
    event EventHandler<SsoSessionExpiringEventArgs>? SessionExpiring;

    /// <summary>
    /// Raised when a session has been refreshed.
    /// </summary>
    event EventHandler<SsoSessionRefreshedEventArgs>? SessionRefreshed;

    /// <summary>
    /// Raised when a session has expired.
    /// </summary>
    event EventHandler<SsoSessionExpiredEventArgs>? SessionExpired;

    /// <summary>
    /// Raised when a session has been revoked.
    /// </summary>
    event EventHandler<SsoSessionRevokedEventArgs>? SessionRevoked;

    #endregion
}
