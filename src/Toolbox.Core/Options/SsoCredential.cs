// @file SsoCredential.cs
// @brief Represents a securely stored credential for SSO
// @details Used by ICredentialStore for credential persistence

namespace Toolbox.Core.Options;

/// <summary>
/// Represents a credential stored in the secure credential store.
/// </summary>
/// <remarks>
/// <para>
/// Credentials are stored securely using platform-specific mechanisms
/// (Windows Credential Manager, macOS Keychain, or encrypted file).
/// </para>
/// <para>
/// Sensitive data like passwords and tokens should be encrypted before storage
/// and decrypted only when needed.
/// </para>
/// </remarks>
public sealed class SsoCredential
{
    /// <summary>
    /// Gets or sets the unique identifier of the user who owns this credential.
    /// </summary>
    /// <value>The user's unique ID from the directory service.</value>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the username associated with this credential.
    /// </summary>
    /// <value>The user's login name or principal name.</value>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of credential.
    /// </summary>
    /// <value>The <see cref="CredentialType"/> identifying the credential format.</value>
    public CredentialType Type { get; init; }

    /// <summary>
    /// Gets or sets the OAuth2/OIDC access token.
    /// </summary>
    /// <value>The access token, or null if not applicable.</value>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the OAuth2/OIDC refresh token.
    /// </summary>
    /// <value>The refresh token, or null if not applicable.</value>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the encrypted password.
    /// </summary>
    /// <value>The encrypted password data, or null if not applicable.</value>
    /// <remarks>
    /// Passwords are encrypted using platform-specific encryption
    /// (DPAPI on Windows, Keychain on macOS).
    /// </remarks>
    public string? EncryptedPassword { get; set; }

    /// <summary>
    /// Gets or sets the X.509 certificate data.
    /// </summary>
    /// <value>The certificate as a byte array (PFX/PKCS#12 format), or null if not applicable.</value>
    public byte[]? CertificateData { get; set; }

    /// <summary>
    /// Gets or sets the certificate thumbprint.
    /// </summary>
    /// <value>The SHA-1 thumbprint of the certificate, or null if not applicable.</value>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this credential was created.
    /// </summary>
    /// <value>The UTC time when the credential was stored.</value>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when this credential expires.
    /// </summary>
    /// <value>The UTC expiration time, or null if the credential doesn't expire.</value>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the directory type that issued this credential.
    /// </summary>
    /// <value>The <see cref="LdapDirectoryType"/> source of the credential.</value>
    public LdapDirectoryType DirectoryType { get; init; }

    /// <summary>
    /// Gets or sets additional metadata for the credential.
    /// </summary>
    /// <value>A dictionary of key-value pairs for custom metadata.</value>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets a value indicating whether this credential has expired.
    /// </summary>
    /// <value><c>true</c> if the credential has expired; otherwise, <c>false</c>.</value>
    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow >= ExpiresAt.Value;

    /// <summary>
    /// Gets a value indicating whether this credential can be refreshed.
    /// </summary>
    /// <value><c>true</c> if a refresh token is available; otherwise, <c>false</c>.</value>
    public bool CanRefresh => !string.IsNullOrEmpty(RefreshToken);

    /// <summary>
    /// Creates a credential key for storage.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="directoryType">The directory type.</param>
    /// <param name="suffix">An optional suffix for the key.</param>
    /// <returns>A unique key for credential storage.</returns>
    public static string CreateKey(string userId, LdapDirectoryType directoryType, string? suffix = null)
    {
        var key = $"{userId}:{directoryType}";
        if (!string.IsNullOrEmpty(suffix))
            key += $":{suffix}";
        return key;
    }

    /// <summary>
    /// Creates a copy of this credential with updated tokens.
    /// </summary>
    /// <param name="accessToken">The new access token.</param>
    /// <param name="refreshToken">The new refresh token.</param>
    /// <param name="expiresAt">The new expiration time.</param>
    /// <returns>A new credential with updated token information.</returns>
    public SsoCredential WithRefreshedTokens(
        string? accessToken,
        string? refreshToken,
        DateTimeOffset? expiresAt)
    {
        return new SsoCredential
        {
            UserId = UserId,
            Username = Username,
            Type = Type,
            AccessToken = accessToken ?? AccessToken,
            RefreshToken = refreshToken ?? RefreshToken,
            EncryptedPassword = EncryptedPassword,
            CertificateData = CertificateData,
            CertificateThumbprint = CertificateThumbprint,
            CreatedAt = CreatedAt,
            ExpiresAt = expiresAt ?? ExpiresAt,
            DirectoryType = DirectoryType,
            Metadata = new Dictionary<string, string>(Metadata)
        };
    }
}
