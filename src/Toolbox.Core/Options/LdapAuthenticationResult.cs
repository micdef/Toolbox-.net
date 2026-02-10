// @file LdapAuthenticationResult.cs
// @brief Result model for LDAP authentication operations
// @details Contains authentication status, user info, tokens, and claims
// @note Used by all LDAP service implementations

namespace Toolbox.Core.Options;

/// <summary>
/// Represents the result of an LDAP authentication operation.
/// </summary>
/// <remarks>
/// <para>
/// This class contains all information resulting from an authentication
/// attempt against an LDAP directory service. It provides:
/// </para>
/// <list type="bullet">
///   <item><description>Authentication status and error information</description></item>
///   <item><description>User identification (username, DN)</description></item>
///   <item><description>Group memberships and claims</description></item>
///   <item><description>Token information (for Azure AD)</description></item>
///   <item><description>Timing information (authentication and expiration)</description></item>
/// </list>
/// </remarks>
public sealed class LdapAuthenticationResult
{
    /// <summary>
    /// Gets or initializes whether the authentication was successful.
    /// </summary>
    /// <value>
    /// <c>true</c> if the user was successfully authenticated;
    /// otherwise, <c>false</c>.
    /// </value>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Gets or initializes the authenticated username.
    /// </summary>
    /// <value>
    /// The username (sAMAccountName, uid, or UPN) of the authenticated user,
    /// or <c>null</c> if authentication failed.
    /// </value>
    public string? Username { get; init; }

    /// <summary>
    /// Gets or initializes the user's distinguished name.
    /// </summary>
    /// <value>
    /// The full LDAP path to the user object (e.g., CN=User,OU=Users,DC=domain,DC=com),
    /// or <c>null</c> if not available or authentication failed.
    /// </value>
    public string? UserDistinguishedName { get; init; }

    /// <summary>
    /// Gets or initializes the user's unique identifier.
    /// </summary>
    /// <value>
    /// For AD: objectGUID, For Azure AD: id, For OpenLDAP: entryUUID.
    /// </value>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets or initializes the user's email address.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Gets or initializes the user's display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets or initializes the authentication mode that was used.
    /// </summary>
    /// <value>
    /// The <see cref="LdapAuthenticationMode"/> that was used for authentication.
    /// </value>
    public LdapAuthenticationMode AuthenticationMode { get; init; }

    /// <summary>
    /// Gets or initializes the error message if authentication failed.
    /// </summary>
    /// <value>
    /// A human-readable description of the error, or <c>null</c> if successful.
    /// </value>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets or initializes the error code if authentication failed.
    /// </summary>
    /// <value>
    /// An LDAP result code or provider-specific error code,
    /// or <c>null</c> if successful.
    /// </value>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets or initializes the groups the user belongs to.
    /// </summary>
    /// <value>
    /// A list of group names or distinguished names, or <c>null</c> if not requested
    /// or authentication failed.
    /// </value>
    public IReadOnlyList<string>? Groups { get; init; }

    /// <summary>
    /// Gets or initializes additional claims extracted from the directory.
    /// </summary>
    /// <value>
    /// A dictionary of claim names and values (e.g., department, title),
    /// or <c>null</c> if not requested or authentication failed.
    /// </value>
    public IReadOnlyDictionary<string, object>? Claims { get; init; }

    /// <summary>
    /// Gets or initializes when the authentication occurred.
    /// </summary>
    /// <value>
    /// The UTC timestamp of successful authentication,
    /// or <c>null</c> if authentication failed.
    /// </value>
    public DateTimeOffset? AuthenticatedAt { get; init; }

    /// <summary>
    /// Gets or initializes when the authentication/token expires.
    /// </summary>
    /// <value>
    /// The UTC timestamp when the session or token expires,
    /// or <c>null</c> if not applicable (e.g., simple bind).
    /// </value>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets or initializes the access token (for OAuth2/Azure AD).
    /// </summary>
    /// <value>
    /// The OAuth2 access token for Azure AD authentication,
    /// or <c>null</c> for traditional LDAP authentication.
    /// </value>
    public string? Token { get; init; }

    /// <summary>
    /// Gets or initializes the refresh token (for OAuth2/Azure AD).
    /// </summary>
    /// <value>
    /// The OAuth2 refresh token for obtaining new access tokens,
    /// or <c>null</c> if not applicable.
    /// </value>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Gets or initializes the directory type that authenticated the user.
    /// </summary>
    public LdapDirectoryType DirectoryType { get; init; }

    /// <summary>
    /// Creates a successful authentication result.
    /// </summary>
    /// <param name="username">The authenticated username.</param>
    /// <param name="mode">The authentication mode used.</param>
    /// <param name="directoryType">The directory type.</param>
    /// <returns>A new <see cref="LdapAuthenticationResult"/> indicating success.</returns>
    public static LdapAuthenticationResult Success(
        string username,
        LdapAuthenticationMode mode,
        LdapDirectoryType directoryType) => new()
    {
        IsAuthenticated = true,
        Username = username,
        AuthenticationMode = mode,
        DirectoryType = directoryType,
        AuthenticatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates a failed authentication result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="mode">The authentication mode attempted.</param>
    /// <param name="directoryType">The directory type.</param>
    /// <returns>A new <see cref="LdapAuthenticationResult"/> indicating failure.</returns>
    public static LdapAuthenticationResult Failure(
        string errorMessage,
        string? errorCode,
        LdapAuthenticationMode mode,
        LdapDirectoryType directoryType) => new()
    {
        IsAuthenticated = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode,
        AuthenticationMode = mode,
        DirectoryType = directoryType
    };
}
