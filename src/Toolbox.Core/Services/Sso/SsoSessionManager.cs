// @file SsoSessionManager.cs
// @brief Implementation of SSO session management
// @details Manages session lifecycle including creation, validation, refresh, and revocation

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Sso;

/// <summary>
/// Manages SSO session lifecycle including creation, validation, refresh, and revocation.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides thread-safe session management using concurrent collections.
/// It integrates with <see cref="ICredentialStore"/> for persistence and
/// <see cref="ITokenRefreshService"/> for automatic token refresh.
/// </para>
/// <para>
/// Sessions are stored in-memory for fast access, with optional persistence to the credential store.
/// Expired sessions are automatically cleaned up by a background timer.
/// </para>
/// </remarks>
public sealed class SsoSessionManager : BaseAsyncDisposableService, ISsoSessionManager
{
    #region Fields

    /// <summary>
    /// The activity source for distributed tracing.
    /// </summary>
    private static new readonly ActivitySource ActivitySource = new(
        TelemetryConstants.ActivitySourceName,
        TelemetryConstants.ActivitySourceVersion);

    /// <summary>
    /// Thread-safe storage for active sessions.
    /// </summary>
    private readonly ConcurrentDictionary<string, SsoSession> _sessions = new();

    /// <summary>
    /// Thread-safe mapping of user IDs to their session IDs.
    /// </summary>
    private readonly ConcurrentDictionary<string, HashSet<string>> _userSessions = new();

    /// <summary>
    /// Lock object for user session set operations.
    /// </summary>
    private readonly object _userSessionsLock = new();

    /// <summary>
    /// The credential store for persisting sessions.
    /// </summary>
    private readonly ICredentialStore? _credentialStore;

    /// <summary>
    /// The token refresh service for automatic refresh.
    /// </summary>
    private readonly ITokenRefreshService? _tokenRefreshService;

    /// <summary>
    /// The SSO session options.
    /// </summary>
    private readonly SsoSessionOptions _options;

    /// <summary>
    /// The logger instance.
    /// </summary>
    private readonly ILogger<SsoSessionManager> _logger;

    /// <summary>
    /// Timer for cleaning up expired sessions.
    /// </summary>
    private Timer? _cleanupTimer;

    /// <summary>
    /// The cleanup interval.
    /// </summary>
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="SsoSessionManager"/> class.
    /// </summary>
    /// <param name="options">The SSO session options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="credentialStore">Optional credential store for persistence.</param>
    /// <param name="tokenRefreshService">Optional token refresh service.</param>
    public SsoSessionManager(
        IOptions<SsoSessionOptions> options,
        ILogger<SsoSessionManager> logger,
        ICredentialStore? credentialStore = null,
        ITokenRefreshService? tokenRefreshService = null)
        : base(nameof(SsoSessionManager), logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
        _credentialStore = credentialStore;
        _tokenRefreshService = tokenRefreshService;

        // Start cleanup timer
        _cleanupTimer = new Timer(
            CleanupExpiredSessions,
            null,
            CleanupInterval,
            CleanupInterval);

        _logger.LogInformation(
            "SsoSessionManager initialized with auto-refresh={AutoRefresh}, threshold={Threshold}",
            _options.EnableAutoRefresh,
            _options.RefreshThreshold);
    }

    #endregion

    #region ISsoSessionManager - Session Creation

    /// <inheritdoc />
    public async Task<SsoSession> CreateSessionAsync(
        LdapAuthenticationResult authResult,
        ILdapService ldapService,
        CancellationToken cancellationToken = default)
    {
        return await CreateSessionAsync(
            authResult,
            ldapService,
            deviceId: null,
            ipAddress: null,
            userAgent: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SsoSession> CreateSessionAsync(
        LdapAuthenticationResult authResult,
        ILdapService ldapService,
        string? deviceId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(authResult);
        ArgumentNullException.ThrowIfNull(ldapService);

        using var activity = ActivitySource.StartActivity("SsoSessionManager.CreateSession");
        activity?.SetTag("ldap.directory_type", authResult.DirectoryType.ToString());
        activity?.SetTag("ldap.user_id", authResult.UserId);

        if (!authResult.IsAuthenticated)
        {
            throw new InvalidOperationException(
                $"Cannot create session from failed authentication: {authResult.ErrorMessage}");
        }

        // Check session limit for user
        var userId = authResult.UserId ?? throw new InvalidOperationException("UserId is required");
        var currentCount = await GetUserSessionCountAsync(userId, cancellationToken).ConfigureAwait(false);

        if (currentCount >= _options.MaxSessionsPerUser)
        {
            _logger.LogWarning(
                "User {UserId} has reached maximum sessions ({Max}), revoking oldest",
                userId,
                _options.MaxSessionsPerUser);

            await RevokeOldestSessionAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        // Create the session
        var session = SsoSession.FromAuthenticationResult(authResult, _options);
        session.DeviceId = deviceId;
        session.IpAddress = ipAddress;
        session.UserAgent = userAgent;

        // Store the session
        _sessions[session.SessionId] = session;
        AddUserSession(userId, session.SessionId);

        // Persist if enabled
        if (_options.PersistSessions && _credentialStore != null)
        {
            var credential = CreateCredentialFromSession(session);
            await _credentialStore.StoreCredentialAsync(
                GetSessionKey(session.SessionId),
                credential,
                cancellationToken).ConfigureAwait(false);
        }

        // Register for auto-refresh if enabled
        if (_options.EnableAutoRefresh && _tokenRefreshService != null && session.RefreshToken != null)
        {
            await _tokenRefreshService.RegisterForRefreshAsync(session, cancellationToken).ConfigureAwait(false);
        }

        // Record telemetry
        ToolboxMeter.RecordSsoSessionCreated(
            "SsoSessionManager",
            authResult.DirectoryType.ToString(),
            userId);

        // Raise event
        OnSessionCreated(new SsoSessionCreatedEventArgs
        {
            Session = session,
            DirectoryType = authResult.DirectoryType
        });

        _logger.LogInformation(
            "Created SSO session {SessionId} for user {UserId}, expires at {ExpiresAt}",
            session.SessionId,
            userId,
            session.ExpiresAt);

        activity?.SetTag("session.id", session.SessionId);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return session;
    }

    #endregion

    #region ISsoSessionManager - Session Validation

    /// <inheritdoc />
    public Task<SsoSessionValidationResult> ValidateSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return ValidateSessionAsync(sessionId, deviceId: null, ipAddress: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SsoSessionValidationResult> ValidateSessionAsync(
        string sessionId,
        string? deviceId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        using var activity = ActivitySource.StartActivity("SsoSessionManager.ValidateSession");
        activity?.SetTag("session.id", sessionId);

        // Try to get session from memory
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            // Try to load from credential store
            if (_credentialStore != null)
            {
                session = await LoadSessionFromStoreAsync(sessionId, cancellationToken).ConfigureAwait(false);
            }

            if (session == null)
            {
                _logger.LogDebug("Session {SessionId} not found", sessionId);
                ToolboxMeter.RecordSsoSessionValidation("SsoSessionManager", false, "NotFound");
                return SsoSessionValidationResult.Failed(SsoValidationFailureReason.SessionNotFound);
            }

            // Re-add to memory cache
            _sessions[sessionId] = session;
            AddUserSession(session.UserId, sessionId);
        }

        // Check session state
        if (session.State == SsoSessionState.Revoked)
        {
            ToolboxMeter.RecordSsoSessionValidation("SsoSessionManager", false, "Revoked");
            return SsoSessionValidationResult.Failed(SsoValidationFailureReason.SessionRevoked);
        }

        if (session.State == SsoSessionState.Expired || session.IsExpired)
        {
            session.State = SsoSessionState.Expired;
            ToolboxMeter.RecordSsoSessionValidation("SsoSessionManager", false, "Expired");
            return SsoSessionValidationResult.Failed(SsoValidationFailureReason.SessionExpired);
        }

        // Check device binding if configured
        if (_options.EnforceDeviceBinding && session.DeviceId != null && deviceId != null)
        {
            if (!string.Equals(session.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Device mismatch for session {SessionId}: expected {Expected}, got {Actual}",
                    sessionId,
                    session.DeviceId,
                    deviceId);
                ToolboxMeter.RecordSsoSessionValidation("SsoSessionManager", false, "DeviceMismatch");
                return SsoSessionValidationResult.Failed(SsoValidationFailureReason.DeviceMismatch);
            }
        }

        // Check IP binding if configured
        if (_options.EnforceIpBinding && session.IpAddress != null && ipAddress != null)
        {
            if (!string.Equals(session.IpAddress, ipAddress, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "IP mismatch for session {SessionId}: expected {Expected}, got {Actual}",
                    sessionId,
                    session.IpAddress,
                    ipAddress);
                ToolboxMeter.RecordSsoSessionValidation("SsoSessionManager", false, "IpMismatch");
                return SsoSessionValidationResult.Failed(SsoValidationFailureReason.IpMismatch);
            }
        }

        // Update last activity for sliding expiration
        if (_options.SlidingExpiration.HasValue)
        {
            await TouchSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }

        ToolboxMeter.RecordSsoSessionValidation("SsoSessionManager", true, null);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return SsoSessionValidationResult.Success(session);
    }

    #endregion

    #region ISsoSessionManager - Session Refresh

    /// <inheritdoc />
    public async Task<SsoSession> RefreshSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        using var activity = ActivitySource.StartActivity("SsoSessionManager.RefreshSession");
        activity?.SetTag("session.id", sessionId);

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        if (session.State == SsoSessionState.Revoked)
        {
            throw new InvalidOperationException($"Session {sessionId} has been revoked");
        }

        if (session.RefreshToken == null)
        {
            throw new InvalidOperationException($"Session {sessionId} has no refresh token");
        }

        var previousExpiresAt = session.ExpiresAt;
        session.State = SsoSessionState.Refreshing;

        try
        {
            // Calculate new expiration
            var now = DateTimeOffset.UtcNow;
            session.ExpiresAt = now.Add(_options.DefaultSessionDuration);
            session.LastRefreshedAt = now;
            session.State = SsoSessionState.Active;

            // Persist if enabled
            if (_options.PersistSessions && _credentialStore != null)
            {
                var credential = CreateCredentialFromSession(session);
                await _credentialStore.StoreCredentialAsync(
                    GetSessionKey(sessionId),
                    credential,
                    cancellationToken).ConfigureAwait(false);
            }

            // Update refresh service registration
            if (_tokenRefreshService != null)
            {
                await _tokenRefreshService.UpdateRegistrationAsync(session, cancellationToken).ConfigureAwait(false);
            }

            // Record telemetry
            ToolboxMeter.RecordSsoSessionRefresh("SsoSessionManager", true);

            // Raise event
            OnSessionRefreshed(new SsoSessionRefreshedEventArgs
            {
                Session = session,
                PreviousExpiresAt = previousExpiresAt,
                NewExpiresAt = session.ExpiresAt
            });

            _logger.LogInformation(
                "Refreshed session {SessionId}, new expiration: {ExpiresAt}",
                sessionId,
                session.ExpiresAt);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return session;
        }
        catch (Exception ex)
        {
            session.State = SsoSessionState.Active; // Revert state
            ToolboxMeter.RecordSsoSessionRefresh("SsoSessionManager", false);
            _logger.LogError(ex, "Failed to refresh session {SessionId}", sessionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task TouchSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        if (!_options.SlidingExpiration.HasValue)
        {
            return;
        }

        session.LastActivityAt = DateTimeOffset.UtcNow;

        // Extend expiration if within sliding window
        var slidingExpiry = session.LastActivityAt.Add(_options.SlidingExpiration.Value);
        if (slidingExpiry > session.ExpiresAt && slidingExpiry <= session.CreatedAt.Add(_options.MaxSessionDuration))
        {
            session.ExpiresAt = slidingExpiry;

            if (_options.PersistSessions && _credentialStore != null)
            {
                var credential = CreateCredentialFromSession(session);
                await _credentialStore.StoreCredentialAsync(
                    GetSessionKey(sessionId),
                    credential,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    #endregion

    #region ISsoSessionManager - Session Revocation

    /// <inheritdoc />
    public async Task RevokeSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        using var activity = ActivitySource.StartActivity("SsoSessionManager.RevokeSession");
        activity?.SetTag("session.id", sessionId);

        if (!_sessions.TryRemove(sessionId, out var session))
        {
            _logger.LogDebug("Session {SessionId} not found for revocation", sessionId);
            return;
        }

        session.State = SsoSessionState.Revoked;
        RemoveUserSession(session.UserId, sessionId);

        // Unregister from refresh
        if (_tokenRefreshService != null)
        {
            await _tokenRefreshService.UnregisterFromRefreshAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }

        // Remove from credential store
        if (_credentialStore != null)
        {
            await _credentialStore.RemoveCredentialAsync(GetSessionKey(sessionId), cancellationToken).ConfigureAwait(false);
        }

        // Raise event
        OnSessionRevoked(new SsoSessionRevokedEventArgs
        {
            SessionId = sessionId,
            UserId = session.UserId,
            RevokedAt = DateTimeOffset.UtcNow
        });

        _logger.LogInformation("Revoked session {SessionId} for user {UserId}", sessionId, session.UserId);
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllUserSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var sessionIds = GetUserSessionIds(userId);
        var count = 0;

        foreach (var sessionId in sessionIds)
        {
            await RevokeSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            count++;
        }

        _logger.LogInformation("Revoked {Count} sessions for user {UserId}", count, userId);
        return count;
    }

    /// <inheritdoc />
    public async Task<int> RevokeOtherSessionsAsync(
        string userId,
        string exceptSessionId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(exceptSessionId);

        var sessionIds = GetUserSessionIds(userId)
            .Where(id => !string.Equals(id, exceptSessionId, StringComparison.Ordinal))
            .ToList();

        var count = 0;
        foreach (var sessionId in sessionIds)
        {
            await RevokeSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            count++;
        }

        _logger.LogInformation(
            "Revoked {Count} other sessions for user {UserId} (kept {SessionId})",
            count,
            userId,
            exceptSessionId);

        return count;
    }

    #endregion

    #region ISsoSessionManager - Session Queries

    /// <inheritdoc />
    public Task<SsoSession?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SsoSession>> GetUserSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var sessionIds = GetUserSessionIds(userId);
        var sessions = sessionIds
            .Select(id => _sessions.TryGetValue(id, out var s) ? s : null)
            .Where(s => s != null)
            .Cast<SsoSession>()
            .ToList();

        return Task.FromResult<IReadOnlyList<SsoSession>>(sessions);
    }

    /// <inheritdoc />
    public Task<int> GetUserSessionCountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var count = GetUserSessionIds(userId).Count;
        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task<int> GetActiveSessionCountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.FromResult(_sessions.Count);
    }

    #endregion

    #region ISsoSessionManager - Events

    /// <inheritdoc />
    public event EventHandler<SsoSessionCreatedEventArgs>? SessionCreated;

    /// <inheritdoc />
    public event EventHandler<SsoSessionExpiringEventArgs>? SessionExpiring;

    /// <inheritdoc />
    public event EventHandler<SsoSessionRefreshedEventArgs>? SessionRefreshed;

    /// <inheritdoc />
    public event EventHandler<SsoSessionExpiredEventArgs>? SessionExpired;

    /// <inheritdoc />
    public event EventHandler<SsoSessionRevokedEventArgs>? SessionRevoked;

    /// <summary>
    /// Raises the <see cref="SessionCreated"/> event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnSessionCreated(SsoSessionCreatedEventArgs e) => SessionCreated?.Invoke(this, e);

    /// <summary>
    /// Raises the <see cref="SessionExpiring"/> event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnSessionExpiring(SsoSessionExpiringEventArgs e) => SessionExpiring?.Invoke(this, e);

    /// <summary>
    /// Raises the <see cref="SessionRefreshed"/> event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnSessionRefreshed(SsoSessionRefreshedEventArgs e) => SessionRefreshed?.Invoke(this, e);

    /// <summary>
    /// Raises the <see cref="SessionExpired"/> event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnSessionExpired(SsoSessionExpiredEventArgs e) => SessionExpired?.Invoke(this, e);

    /// <summary>
    /// Raises the <see cref="SessionRevoked"/> event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnSessionRevoked(SsoSessionRevokedEventArgs e) => SessionRevoked?.Invoke(this, e);

    #endregion

    #region Private Methods

    /// <summary>
    /// Adds a session ID to the user's session set.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="sessionId">The session ID.</param>
    private void AddUserSession(string userId, string sessionId)
    {
        lock (_userSessionsLock)
        {
            if (!_userSessions.TryGetValue(userId, out var sessions))
            {
                sessions = [];
                _userSessions[userId] = sessions;
            }
            sessions.Add(sessionId);
        }
    }

    /// <summary>
    /// Removes a session ID from the user's session set.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="sessionId">The session ID.</param>
    private void RemoveUserSession(string userId, string sessionId)
    {
        lock (_userSessionsLock)
        {
            if (_userSessions.TryGetValue(userId, out var sessions))
            {
                sessions.Remove(sessionId);
                if (sessions.Count == 0)
                {
                    _userSessions.TryRemove(userId, out _);
                }
            }
        }
    }

    /// <summary>
    /// Gets all session IDs for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The list of session IDs.</returns>
    private List<string> GetUserSessionIds(string userId)
    {
        lock (_userSessionsLock)
        {
            if (_userSessions.TryGetValue(userId, out var sessions))
            {
                return [.. sessions];
            }
            return [];
        }
    }

    /// <summary>
    /// Revokes the oldest session for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    private async Task RevokeOldestSessionAsync(string userId, CancellationToken cancellationToken)
    {
        var sessions = await GetUserSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
        var oldest = sessions.OrderBy(s => s.CreatedAt).FirstOrDefault();

        if (oldest != null)
        {
            await RevokeSessionAsync(oldest.SessionId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates a credential from a session.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <returns>The credential.</returns>
    private static SsoCredential CreateCredentialFromSession(SsoSession session)
    {
        return new SsoCredential
        {
            UserId = session.UserId,
            Type = CredentialType.AccessToken,
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            ExpiresAt = session.ExpiresAt,
            DirectoryType = session.DirectoryType,
            Metadata = new Dictionary<string, string>
            {
                ["SessionId"] = session.SessionId,
                ["Username"] = session.Username,
                ["CreatedAt"] = session.CreatedAt.ToString("O"),
                ["State"] = session.State.ToString()
            }
        };
    }

    /// <summary>
    /// Loads a session from the credential store.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The session, or null if not found.</returns>
    private async Task<SsoSession?> LoadSessionFromStoreAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (_credentialStore == null)
        {
            return null;
        }

        var credential = await _credentialStore.GetCredentialAsync(
            GetSessionKey(sessionId),
            cancellationToken).ConfigureAwait(false);

        if (credential == null)
        {
            return null;
        }

        // Reconstruct session from credential
        var metadata = credential.Metadata ?? new Dictionary<string, string>();
        return new SsoSession
        {
            SessionId = sessionId,
            UserId = credential.UserId,
            Username = metadata.TryGetValue("Username", out var username) ? username : credential.UserId,
            DirectoryType = credential.DirectoryType,
            AccessToken = credential.AccessToken,
            RefreshToken = credential.RefreshToken,
            ExpiresAt = credential.ExpiresAt ?? DateTimeOffset.UtcNow,
            CreatedAt = metadata.TryGetValue("CreatedAt", out var created)
                ? DateTimeOffset.Parse(created)
                : DateTimeOffset.UtcNow,
            State = metadata.TryGetValue("State", out var state)
                ? Enum.Parse<SsoSessionState>(state)
                : SsoSessionState.Active
        };
    }

    /// <summary>
    /// Gets the credential store key for a session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <returns>The credential key.</returns>
    private static string GetSessionKey(string sessionId) => $"sso:session:{sessionId}";

    /// <summary>
    /// Timer callback for cleaning up expired sessions.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private void CleanupExpiredSessions(object? state)
    {
        if (IsDisposed)
        {
            return;
        }

        var expiredSessions = _sessions
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in expiredSessions)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                session.State = SsoSessionState.Expired;
                RemoveUserSession(session.UserId, sessionId);

                // Record telemetry
                ToolboxMeter.RecordSsoSessionExpired("SsoSessionManager", session.UserId);

                // Raise event
                OnSessionExpired(new SsoSessionExpiredEventArgs
                {
                    SessionId = sessionId,
                    UserId = session.UserId,
                    ExpiredAt = DateTimeOffset.UtcNow,
                    WasRevoked = false
                });

                _logger.LogDebug("Cleaned up expired session {SessionId}", sessionId);
            }
        }

        if (expiredSessions.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
        }
    }

    #endregion

    #region Disposal

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        if (_cleanupTimer != null)
        {
            await _cleanupTimer.DisposeAsync().ConfigureAwait(false);
            _cleanupTimer = null;
        }

        _sessions.Clear();
        _userSessions.Clear();

        _logger.LogInformation("SsoSessionManager disposed");
    }

    #endregion
}
