// @file SsoSessionOptions.cs
// @brief Configuration options for SSO session management
// @details Configures session duration, refresh behavior, and cleanup policies

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for SSO session management.
/// </summary>
/// <remarks>
/// <para>
/// These options control the behavior of the <see cref="Abstractions.Services.ISsoSessionManager"/>
/// including session lifetime, automatic refresh, and cleanup policies.
/// </para>
/// <para>
/// Configuration section: <c>Toolbox:Sso</c>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddSsoServices(options =>
/// {
///     options.DefaultSessionDuration = TimeSpan.FromHours(8);
///     options.EnableAutoRefresh = true;
///     options.RefreshThreshold = 0.8;
/// });
/// </code>
/// </example>
public sealed class SsoSessionOptions
{
    /// <summary>
    /// The configuration section name for these options.
    /// </summary>
    public const string SectionName = "Toolbox:Sso";

    /// <summary>
    /// Gets or sets the default session duration when not specified by the directory.
    /// </summary>
    /// <value>Default is 8 hours.</value>
    public TimeSpan DefaultSessionDuration { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Gets or sets the maximum allowed session duration.
    /// </summary>
    /// <value>Default is 7 days.</value>
    /// <remarks>
    /// Sessions cannot exceed this duration regardless of token expiration or refresh.
    /// </remarks>
    public TimeSpan MaxSessionDuration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the sliding expiration window.
    /// </summary>
    /// <value>Default is 30 minutes. Set to null to disable sliding expiration.</value>
    /// <remarks>
    /// When sliding expiration is enabled, the session expiration is extended by this
    /// amount when activity is detected within the window.
    /// </remarks>
    public TimeSpan? SlidingExpiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the threshold for triggering token refresh.
    /// </summary>
    /// <value>
    /// A value between 0.0 and 1.0 representing the fraction of session lifetime
    /// at which to trigger refresh. Default is 0.8 (80%).
    /// </value>
    /// <remarks>
    /// For example, with a threshold of 0.8 and an 8-hour session,
    /// refresh will be triggered after 6.4 hours.
    /// </remarks>
    public double RefreshThreshold { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets the interval for checking sessions that need refresh.
    /// </summary>
    /// <value>Default is 1 minute.</value>
    public TimeSpan RefreshCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the time before expiration to raise the SessionExpiring event.
    /// </summary>
    /// <value>Default is 5 minutes.</value>
    public TimeSpan ExpirationWarningTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether automatic token refresh is enabled.
    /// </summary>
    /// <value>Default is <c>true</c>.</value>
    public bool EnableAutoRefresh { get; set; } = true;

    /// <summary>
    /// Gets or sets whether sessions should be persisted to the credential store.
    /// </summary>
    /// <value>Default is <c>true</c>.</value>
    public bool PersistSessions { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent sessions per user.
    /// </summary>
    /// <value>Default is 5. Set to 0 for unlimited.</value>
    public int MaxSessionsPerUser { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to revoke the oldest session when max is reached.
    /// </summary>
    /// <value>
    /// Default is <c>true</c>. When <c>false</c>, new session creation will fail
    /// if the user has reached <see cref="MaxSessionsPerUser"/>.
    /// </value>
    public bool RevokeOldestOnMaxReached { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval for cleaning up expired sessions.
    /// </summary>
    /// <value>Default is 15 minutes.</value>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets whether to bind sessions to device/IP.
    /// </summary>
    /// <value>Default is <c>false</c>.</value>
    /// <remarks>
    /// When enabled, sessions can only be used from the same device/IP
    /// that created them.
    /// </remarks>
    public bool BindToDevice { get; set; }

    /// <summary>
    /// Gets or sets whether to enforce device binding during validation.
    /// </summary>
    /// <value>Default is <c>false</c>.</value>
    /// <remarks>
    /// When enabled, session validation will fail if the request comes
    /// from a different device than the one that created the session.
    /// </remarks>
    public bool EnforceDeviceBinding { get; set; }

    /// <summary>
    /// Gets or sets whether to enforce IP address binding during validation.
    /// </summary>
    /// <value>Default is <c>false</c>.</value>
    /// <remarks>
    /// When enabled, session validation will fail if the request comes
    /// from a different IP address than the one that created the session.
    /// </remarks>
    public bool EnforceIpBinding { get; set; }

    /// <summary>
    /// Gets or sets whether to validate sessions on every access.
    /// </summary>
    /// <value>Default is <c>false</c>.</value>
    /// <remarks>
    /// When enabled, sessions are re-validated against the directory on each access.
    /// This provides stronger security but increases load on the directory.
    /// </remarks>
    public bool ValidateOnAccess { get; set; }

    /// <summary>
    /// Gets or sets the maximum retry count for token refresh.
    /// </summary>
    /// <value>Default is 3.</value>
    public int MaxRefreshRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between refresh retries.
    /// </summary>
    /// <value>Default is 5 seconds.</value>
    public TimeSpan RefreshRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets whether to use exponential backoff for refresh retries.
    /// </summary>
    /// <value>Default is <c>true</c>.</value>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Validates the options configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configuration is invalid.
    /// </exception>
    public void Validate()
    {
        if (DefaultSessionDuration <= TimeSpan.Zero)
            throw new InvalidOperationException("DefaultSessionDuration must be positive.");

        if (MaxSessionDuration <= TimeSpan.Zero)
            throw new InvalidOperationException("MaxSessionDuration must be positive.");

        if (MaxSessionDuration < DefaultSessionDuration)
            throw new InvalidOperationException("MaxSessionDuration must be greater than or equal to DefaultSessionDuration.");

        if (RefreshThreshold is < 0.0 or > 1.0)
            throw new InvalidOperationException("RefreshThreshold must be between 0.0 and 1.0.");

        if (RefreshCheckInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("RefreshCheckInterval must be positive.");

        if (MaxSessionsPerUser < 0)
            throw new InvalidOperationException("MaxSessionsPerUser cannot be negative.");

        if (CleanupInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("CleanupInterval must be positive.");

        if (MaxRefreshRetries < 0)
            throw new InvalidOperationException("MaxRefreshRetries cannot be negative.");

        if (RefreshRetryDelay < TimeSpan.Zero)
            throw new InvalidOperationException("RefreshRetryDelay cannot be negative.");
    }
}
