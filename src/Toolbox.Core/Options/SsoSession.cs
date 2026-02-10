// @file SsoSession.cs
// @brief Represents an active SSO session with identity, tokens, and claims
// @details Core DTO for session management in the SSO system

namespace Toolbox.Core.Options;

/// <summary>
/// Represents an active Single Sign-On (SSO) session.
/// </summary>
/// <remarks>
/// <para>
/// An SSO session encapsulates all information about an authenticated user's session,
/// including identity information, authentication tokens, and session metadata.
/// </para>
/// <para>
/// Sessions are created from <see cref="LdapAuthenticationResult"/> objects and can be
/// refreshed, validated, and revoked through the <see cref="Abstractions.Services.ISsoSessionManager"/>.
/// </para>
/// </remarks>
public sealed class SsoSession
{
    /// <summary>
    /// Gets the unique identifier for this session.
    /// </summary>
    /// <value>A GUID string that uniquely identifies this session.</value>
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the unique identifier of the authenticated user.
    /// </summary>
    /// <value>The user's unique ID from the directory service.</value>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the username of the authenticated user.
    /// </summary>
    /// <value>The user's login name or principal name.</value>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets the distinguished name of the authenticated user.
    /// </summary>
    /// <value>The full DN for LDAP directories, or null for non-LDAP sources.</value>
    public string? UserDistinguishedName { get; init; }

    /// <summary>
    /// Gets the email address of the authenticated user.
    /// </summary>
    /// <value>The user's email address, if available.</value>
    public string? Email { get; init; }

    /// <summary>
    /// Gets the display name of the authenticated user.
    /// </summary>
    /// <value>The user's friendly display name.</value>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the type of directory that authenticated the user.
    /// </summary>
    /// <value>The <see cref="LdapDirectoryType"/> that created this session.</value>
    public LdapDirectoryType DirectoryType { get; init; }

    /// <summary>
    /// Gets the authentication mode used to create this session.
    /// </summary>
    /// <value>The <see cref="LdapAuthenticationMode"/> used for authentication.</value>
    public LdapAuthenticationMode AuthenticationMode { get; init; }

    /// <summary>
    /// Gets or sets the OAuth2/OIDC access token.
    /// </summary>
    /// <value>The access token, or null if not using token-based authentication.</value>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the OAuth2/OIDC refresh token.
    /// </summary>
    /// <value>The refresh token for obtaining new access tokens, or null if not available.</value>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets the timestamp when the session was created.
    /// </summary>
    /// <value>The UTC time when the session was created.</value>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the session expires.
    /// </summary>
    /// <value>The UTC time when the session will expire.</value>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the session tokens were last refreshed.
    /// </summary>
    /// <value>The UTC time of the last token refresh, or null if never refreshed.</value>
    public DateTimeOffset? LastRefreshedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last user activity.
    /// </summary>
    /// <value>The UTC time of the last activity, used for sliding expiration.</value>
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the groups the user belongs to.
    /// </summary>
    /// <value>List of group names or DNs, or null if not retrieved.</value>
    public IReadOnlyList<string>? Groups { get; init; }

    /// <summary>
    /// Gets the additional claims for the user.
    /// </summary>
    /// <value>Dictionary of claim name/value pairs, or null if not retrieved.</value>
    public IReadOnlyDictionary<string, object>? Claims { get; init; }

    /// <summary>
    /// Gets or sets the current state of the session.
    /// </summary>
    /// <value>The <see cref="SsoSessionState"/> indicating the session's lifecycle state.</value>
    public SsoSessionState State { get; set; } = SsoSessionState.Active;

    /// <summary>
    /// Gets or sets the device identifier bound to this session.
    /// </summary>
    /// <value>The device ID, or null if not bound to a device.</value>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the IP address from which the session was created.
    /// </summary>
    /// <value>The client IP address, or null if not tracked.</value>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the user agent string from the client.
    /// </summary>
    /// <value>The HTTP User-Agent, or null if not tracked.</value>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets a value indicating whether the session has expired.
    /// </summary>
    /// <value><c>true</c> if the session has expired; otherwise, <c>false</c>.</value>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    /// <summary>
    /// Gets a value indicating whether the session is still active and valid.
    /// </summary>
    /// <value><c>true</c> if the session is active and not expired; otherwise, <c>false</c>.</value>
    public bool IsActive => State == SsoSessionState.Active && !IsExpired;

    /// <summary>
    /// Gets the remaining time before the session expires.
    /// </summary>
    /// <value>The time remaining, or <see cref="TimeSpan.Zero"/> if expired.</value>
    public TimeSpan TimeToExpiry
    {
        get
        {
            var remaining = ExpiresAt - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Determines whether the session needs to be refreshed based on the given threshold.
    /// </summary>
    /// <param name="threshold">
    /// The threshold as a fraction of the session lifetime (0.0-1.0).
    /// For example, 0.8 means refresh when 80% of the lifetime has elapsed.
    /// </param>
    /// <returns><c>true</c> if the session needs refreshing; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="threshold"/> is not between 0.0 and 1.0.
    /// </exception>
    public bool NeedsRefresh(double threshold)
    {
        if (threshold is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be between 0.0 and 1.0.");

        if (State != SsoSessionState.Active)
            return false;

        var totalLifetime = ExpiresAt - CreatedAt;
        var elapsedTime = DateTimeOffset.UtcNow - CreatedAt;
        var elapsedFraction = elapsedTime.TotalSeconds / totalLifetime.TotalSeconds;

        return elapsedFraction >= threshold;
    }

    /// <summary>
    /// Gets the elapsed fraction of the session lifetime.
    /// </summary>
    /// <returns>A value between 0.0 and 1.0 representing the elapsed fraction.</returns>
    public double GetElapsedFraction()
    {
        var totalLifetime = ExpiresAt - CreatedAt;
        if (totalLifetime <= TimeSpan.Zero)
            return 1.0;

        var elapsedTime = DateTimeOffset.UtcNow - CreatedAt;
        var fraction = elapsedTime.TotalSeconds / totalLifetime.TotalSeconds;
        return Math.Clamp(fraction, 0.0, 1.0);
    }

    /// <summary>
    /// Creates an SSO session from an LDAP authentication result.
    /// </summary>
    /// <param name="result">The authentication result to convert.</param>
    /// <param name="options">The session options for configuration.</param>
    /// <returns>A new <see cref="SsoSession"/> instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="result"/> or <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the authentication result indicates failure.
    /// </exception>
    public static SsoSession FromAuthenticationResult(
        LdapAuthenticationResult result,
        SsoSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);

        if (!result.IsAuthenticated)
            throw new InvalidOperationException("Cannot create session from failed authentication result.");

        var now = DateTimeOffset.UtcNow;
        var expiresAt = result.ExpiresAt ?? now.Add(options.DefaultSessionDuration);

        // Cap at max session duration
        var maxExpiry = now.Add(options.MaxSessionDuration);
        if (expiresAt > maxExpiry)
            expiresAt = maxExpiry;

        return new SsoSession
        {
            UserId = result.Username ?? string.Empty,
            Username = result.Username ?? string.Empty,
            UserDistinguishedName = result.UserDistinguishedName,
            DirectoryType = result.DirectoryType,
            AuthenticationMode = result.AuthenticationMode,
            AccessToken = result.Token,
            RefreshToken = result.RefreshToken,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            Groups = result.Groups,
            Claims = result.Claims,
            State = SsoSessionState.Active
        };
    }

    /// <summary>
    /// Converts this session to a credential for storage.
    /// </summary>
    /// <returns>A <see cref="SsoCredential"/> representing this session's tokens.</returns>
    public SsoCredential ToCredential()
    {
        return new SsoCredential
        {
            UserId = UserId,
            Username = Username,
            Type = AccessToken != null ? CredentialType.AccessToken : CredentialType.IntegratedWindows,
            AccessToken = AccessToken,
            RefreshToken = RefreshToken,
            ExpiresAt = ExpiresAt,
            DirectoryType = DirectoryType
        };
    }

    /// <summary>
    /// Creates a copy of this session with updated tokens.
    /// </summary>
    /// <param name="accessToken">The new access token.</param>
    /// <param name="refreshToken">The new refresh token.</param>
    /// <param name="expiresAt">The new expiration time.</param>
    /// <returns>A new session with updated token information.</returns>
    public SsoSession WithRefreshedTokens(
        string? accessToken,
        string? refreshToken,
        DateTimeOffset expiresAt)
    {
        return new SsoSession
        {
            SessionId = SessionId,
            UserId = UserId,
            Username = Username,
            UserDistinguishedName = UserDistinguishedName,
            Email = Email,
            DisplayName = DisplayName,
            DirectoryType = DirectoryType,
            AuthenticationMode = AuthenticationMode,
            AccessToken = accessToken ?? AccessToken,
            RefreshToken = refreshToken ?? RefreshToken,
            CreatedAt = CreatedAt,
            ExpiresAt = expiresAt,
            LastRefreshedAt = DateTimeOffset.UtcNow,
            LastActivityAt = LastActivityAt,
            Groups = Groups,
            Claims = Claims,
            State = SsoSessionState.Active,
            DeviceId = DeviceId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
    }
}
