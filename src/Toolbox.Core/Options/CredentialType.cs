// @file CredentialType.cs
// @brief Enumeration defining the types of credentials that can be stored
// @details Used by SsoCredential to identify the credential format

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the type of credential stored in the credential store.
/// </summary>
/// <remarks>
/// Different credential types require different handling for storage,
/// retrieval, and validation.
/// </remarks>
public enum CredentialType
{
    /// <summary>
    /// Username and password combination.
    /// </summary>
    UsernamePassword = 0,

    /// <summary>
    /// OAuth2/OIDC access token.
    /// </summary>
    AccessToken = 1,

    /// <summary>
    /// OAuth2/OIDC refresh token.
    /// </summary>
    RefreshToken = 2,

    /// <summary>
    /// X.509 client certificate.
    /// </summary>
    Certificate = 3,

    /// <summary>
    /// Kerberos ticket or credential.
    /// </summary>
    Kerberos = 4,

    /// <summary>
    /// Windows integrated authentication credential.
    /// </summary>
    IntegratedWindows = 5
}
