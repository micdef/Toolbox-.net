// @file SsoSessionEventArgs.cs
// @brief Event argument classes for SSO session events
// @details Used to notify subscribers of session lifecycle events

namespace Toolbox.Core.Options;

/// <summary>
/// Event arguments for when a session is about to expire.
/// </summary>
/// <remarks>
/// This event is raised when a session enters the expiring state,
/// giving subscribers a chance to refresh or save work.
/// </remarks>
public sealed class SsoSessionExpiringEventArgs : EventArgs
{
    /// <summary>
    /// Gets the session that is about to expire.
    /// </summary>
    public SsoSession Session { get; init; } = null!;

    /// <summary>
    /// Gets the time remaining before the session expires.
    /// </summary>
    public TimeSpan TimeToExpiry { get; init; }
}

/// <summary>
/// Event arguments for when a session has been refreshed.
/// </summary>
/// <remarks>
/// This event is raised after a successful token refresh,
/// providing the old and new expiration times.
/// </remarks>
public sealed class SsoSessionRefreshedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the refreshed session.
    /// </summary>
    public SsoSession Session { get; init; } = null!;

    /// <summary>
    /// Gets the previous expiration time before refresh.
    /// </summary>
    public DateTimeOffset PreviousExpiresAt { get; init; }

    /// <summary>
    /// Gets the new expiration time after refresh.
    /// </summary>
    public DateTimeOffset NewExpiresAt { get; init; }
}

/// <summary>
/// Event arguments for when a session has expired.
/// </summary>
/// <remarks>
/// This event is raised when a session has naturally expired
/// or was forcibly expired during cleanup.
/// </remarks>
public sealed class SsoSessionExpiredEventArgs : EventArgs
{
    /// <summary>
    /// Gets the ID of the expired session.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user ID associated with the expired session.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the time when the session expired.
    /// </summary>
    public DateTimeOffset ExpiredAt { get; init; }

    /// <summary>
    /// Gets a value indicating whether the session was revoked (vs naturally expired).
    /// </summary>
    public bool WasRevoked { get; init; }
}

/// <summary>
/// Event arguments for when a token refresh is needed.
/// </summary>
/// <remarks>
/// This event is raised when the token refresh service determines
/// that a session's tokens need to be refreshed.
/// </remarks>
public sealed class TokenRefreshNeededEventArgs : EventArgs
{
    /// <summary>
    /// Gets the session that needs refresh.
    /// </summary>
    public SsoSession Session { get; init; } = null!;

    /// <summary>
    /// Gets the fraction of session lifetime that has elapsed.
    /// </summary>
    /// <value>A value between 0.0 and 1.0.</value>
    public double LifetimeElapsedPercent { get; init; }

    /// <summary>
    /// Gets the time remaining before the session expires.
    /// </summary>
    public TimeSpan TimeToExpiry { get; init; }
}

/// <summary>
/// Event arguments for when a token refresh has completed successfully.
/// </summary>
public sealed class TokenRefreshCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the ID of the refreshed session.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the new access token.
    /// </summary>
    public string? NewAccessToken { get; init; }

    /// <summary>
    /// Gets the new expiration time.
    /// </summary>
    public DateTimeOffset NewExpiresAt { get; init; }

    /// <summary>
    /// Gets the duration of the refresh operation.
    /// </summary>
    public TimeSpan RefreshDuration { get; init; }
}

/// <summary>
/// Event arguments for when a token refresh has failed.
/// </summary>
public sealed class TokenRefreshFailedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the ID of the session that failed to refresh.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exception that caused the failure.
    /// </summary>
    public Exception Exception { get; init; } = null!;

    /// <summary>
    /// Gets a value indicating whether the refresh will be retried.
    /// </summary>
    public bool WillRetry { get; init; }

    /// <summary>
    /// Gets the current retry attempt number.
    /// </summary>
    public int RetryAttempt { get; init; }

    /// <summary>
    /// Gets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; init; }
}

/// <summary>
/// Event arguments for when a session is created.
/// </summary>
public sealed class SsoSessionCreatedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the newly created session.
    /// </summary>
    public SsoSession Session { get; init; } = null!;

    /// <summary>
    /// Gets the directory type that authenticated the user.
    /// </summary>
    public LdapDirectoryType DirectoryType { get; init; }
}

/// <summary>
/// Event arguments for when a session is revoked.
/// </summary>
public sealed class SsoSessionRevokedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the ID of the revoked session.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user ID associated with the revoked session.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the reason for revocation.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the time when the session was revoked.
    /// </summary>
    public DateTimeOffset RevokedAt { get; init; } = DateTimeOffset.UtcNow;
}
