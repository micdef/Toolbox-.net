// @file LdapAuthenticationOptions.cs
// @brief Options for LDAP authentication operations
// @details Configures authentication mode, credentials, certificates, and behavior
// @note Used by the AuthenticateAsync method in LDAP services

using System.Security.Cryptography.X509Certificates;

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies options for LDAP authentication operations.
/// </summary>
/// <remarks>
/// <para>
/// This class provides configuration for authenticating against LDAP
/// directory services. Different properties are required depending
/// on the selected <see cref="Mode"/>:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Mode</term>
///     <description>Required Properties</description>
///   </listheader>
///   <item>
///     <term><see cref="LdapAuthenticationMode.Simple"/></term>
///     <description><see cref="Username"/>, <see cref="Password"/></description>
///   </item>
///   <item>
///     <term><see cref="LdapAuthenticationMode.Kerberos"/></term>
///     <description>None (uses current context) or <see cref="Username"/>, <see cref="Password"/></description>
///   </item>
///   <item>
///     <term><see cref="LdapAuthenticationMode.Certificate"/></term>
///     <description><see cref="Certificate"/> or <see cref="CertificatePath"/></description>
///   </item>
/// </list>
/// </remarks>
public sealed class LdapAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the authentication mode to use.
    /// </summary>
    /// <value>
    /// The <see cref="LdapAuthenticationMode"/> to use. Defaults to <see cref="LdapAuthenticationMode.Simple"/>.
    /// </value>
    public LdapAuthenticationMode Mode { get; set; } = LdapAuthenticationMode.Simple;

    /// <summary>
    /// Gets or sets the username for authentication.
    /// </summary>
    /// <value>
    /// The username, sAMAccountName, user principal name (UPN), or distinguished name.
    /// Format depends on the directory type.
    /// </value>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for authentication.
    /// </summary>
    /// <value>
    /// The user's password. Should be handled securely.
    /// </value>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the domain for Windows authentication.
    /// </summary>
    /// <value>
    /// The NetBIOS domain name (e.g., DOMAIN) or DNS domain name (e.g., domain.com).
    /// Used with Kerberos, NTLM, and Negotiate modes.
    /// </value>
    public string? Domain { get; set; }

    /// <summary>
    /// Gets or sets the X.509 certificate for certificate-based authentication.
    /// </summary>
    /// <value>
    /// The client certificate containing the private key.
    /// Takes precedence over <see cref="CertificatePath"/>.
    /// </value>
    public X509Certificate2? Certificate { get; set; }

    /// <summary>
    /// Gets or sets the path to the certificate file.
    /// </summary>
    /// <value>
    /// Path to a .pfx or .p12 file containing the certificate and private key.
    /// </value>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the password for the certificate file.
    /// </summary>
    /// <value>
    /// The password to decrypt the certificate file.
    /// </value>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Gets or sets whether to include group memberships in the result.
    /// </summary>
    /// <value>
    /// <c>true</c> to retrieve the user's group memberships;
    /// otherwise, <c>false</c>. Defaults to <c>false</c>.
    /// </value>
    public bool IncludeGroups { get; set; }

    /// <summary>
    /// Gets or sets whether to include additional claims in the result.
    /// </summary>
    /// <value>
    /// <c>true</c> to retrieve additional user attributes as claims;
    /// otherwise, <c>false</c>. Defaults to <c>false</c>.
    /// </value>
    public bool IncludeClaims { get; set; }

    /// <summary>
    /// Gets or sets the claims to retrieve when <see cref="IncludeClaims"/> is true.
    /// </summary>
    /// <value>
    /// List of attribute names to retrieve (e.g., "department", "title").
    /// If empty, common attributes are retrieved.
    /// </value>
    public IList<string> ClaimAttributes { get; set; } = [];

    /// <summary>
    /// Gets or sets the authentication timeout.
    /// </summary>
    /// <value>
    /// The maximum time to wait for authentication. Defaults to 30 seconds.
    /// </value>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the SASL mechanism name for SASL authentication.
    /// </summary>
    /// <value>
    /// The SASL mechanism (e.g., "PLAIN", "EXTERNAL", "GSSAPI").
    /// Automatically set based on <see cref="Mode"/> if not specified.
    /// </value>
    public string? SaslMechanism { get; set; }

    /// <summary>
    /// Gets or sets the service principal name (SPN) for Kerberos.
    /// </summary>
    /// <value>
    /// The SPN in the format "LDAP/hostname" or "LDAP/hostname.domain.com".
    /// </value>
    public string? ServicePrincipalName { get; set; }

    /// <summary>
    /// Loads the certificate from the specified path if not already loaded.
    /// </summary>
    /// <returns>The loaded <see cref="X509Certificate2"/>, or <c>null</c> if not configured.</returns>
    /// <exception cref="FileNotFoundException">The certificate file was not found.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The certificate could not be loaded or the password is incorrect.
    /// </exception>
    public X509Certificate2? GetCertificate()
    {
        if (Certificate != null)
            return Certificate;

        if (string.IsNullOrEmpty(CertificatePath))
            return null;

        if (!File.Exists(CertificatePath))
            throw new FileNotFoundException("Certificate file not found.", CertificatePath);

        return X509CertificateLoader.LoadPkcs12FromFile(
            CertificatePath,
            CertificatePassword);
    }

    /// <summary>
    /// Validates the options for the specified authentication mode.
    /// </summary>
    /// <exception cref="InvalidOperationException">Required options are missing.</exception>
    public void Validate()
    {
        switch (Mode)
        {
            case LdapAuthenticationMode.Simple:
            case LdapAuthenticationMode.SaslPlain:
                if (string.IsNullOrEmpty(Username))
                    throw new InvalidOperationException("Username is required for simple authentication.");
                if (string.IsNullOrEmpty(Password))
                    throw new InvalidOperationException("Password is required for simple authentication.");
                break;

            case LdapAuthenticationMode.Certificate:
            case LdapAuthenticationMode.SaslExternal:
                if (Certificate == null && string.IsNullOrEmpty(CertificatePath))
                    throw new InvalidOperationException("Certificate or CertificatePath is required for certificate authentication.");
                break;

            case LdapAuthenticationMode.Ntlm:
            case LdapAuthenticationMode.Negotiate:
                // Can use current context or explicit credentials
                break;

            case LdapAuthenticationMode.Kerberos:
            case LdapAuthenticationMode.SaslGssapi:
            case LdapAuthenticationMode.IntegratedWindows:
            case LdapAuthenticationMode.Anonymous:
                // No credentials required
                break;
        }
    }
}
