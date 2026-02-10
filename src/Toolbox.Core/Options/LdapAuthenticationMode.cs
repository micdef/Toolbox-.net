// @file LdapAuthenticationMode.cs
// @brief Enumeration of LDAP authentication modes
// @details Defines generic and specific authentication methods for LDAP services
// @note Includes Windows-specific (Kerberos, NTLM), SASL, and certificate-based modes

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the authentication mode for LDAP directory services.
/// </summary>
/// <remarks>
/// <para>
/// This enumeration defines how the application authenticates
/// with LDAP directory services. Modes are grouped by category:
/// </para>
/// <list type="bullet">
///   <item><description>Generic modes (0-9): Work across all directory types</description></item>
///   <item><description>Windows/AD modes (10-19): Specific to Active Directory</description></item>
///   <item><description>Certificate modes (20-29): X.509 certificate authentication</description></item>
///   <item><description>SASL modes (30-39): Specific to OpenLDAP and Apple Directory</description></item>
/// </list>
/// <para>
/// Not all modes are supported by all directory services. Use
/// <c>GetSupportedAuthenticationModes()</c> to check availability.
/// </para>
/// </remarks>
public enum LdapAuthenticationMode
{
    #region Generic Modes (0-9)

    /// <summary>
    /// Simple bind authentication with DN and password.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The most common authentication method. Uses a distinguished name
    /// (or username) and password to authenticate.
    /// </para>
    /// <para>
    /// Supported by: Active Directory, OpenLDAP, Apple Directory.
    /// </para>
    /// </remarks>
    Simple = 0,

    /// <summary>
    /// Anonymous bind without credentials.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Connects to the directory without providing credentials.
    /// Only operations permitted for anonymous users will succeed.
    /// </para>
    /// <para>
    /// Supported by: Active Directory, OpenLDAP, Apple Directory.
    /// </para>
    /// </remarks>
    Anonymous = 1,

    #endregion

    #region Windows/Active Directory Modes (10-19)

    /// <summary>
    /// Kerberos authentication via GSSAPI/SPNEGO.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses Kerberos tickets for authentication. Requires a valid
    /// Kerberos ticket (TGT) from the Key Distribution Center (KDC).
    /// </para>
    /// <para>
    /// Supported by: Active Directory (primary), OpenLDAP (with GSSAPI).
    /// </para>
    /// </remarks>
    Kerberos = 10,

    /// <summary>
    /// NTLM authentication (legacy Windows).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses NT LAN Manager protocol for authentication.
    /// Legacy protocol, prefer Kerberos or Negotiate when possible.
    /// </para>
    /// <para>
    /// Supported by: Active Directory only.
    /// </para>
    /// </remarks>
    Ntlm = 11,

    /// <summary>
    /// Negotiate authentication (auto-selects Kerberos or NTLM).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Automatically negotiates the best available authentication
    /// method between Kerberos and NTLM. Prefers Kerberos when available.
    /// </para>
    /// <para>
    /// Supported by: Active Directory only.
    /// </para>
    /// </remarks>
    Negotiate = 12,

    /// <summary>
    /// Integrated Windows authentication using current process credentials.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses the security context of the current Windows process.
    /// No explicit credentials are required; uses the logged-in user's
    /// or service account's credentials.
    /// </para>
    /// <para>
    /// Supported by: Active Directory only (Windows platform).
    /// </para>
    /// </remarks>
    IntegratedWindows = 13,

    #endregion

    #region Certificate Modes (20-29)

    /// <summary>
    /// X.509 client certificate authentication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses a client certificate for authentication. The certificate
    /// must be trusted by the directory server and contain appropriate
    /// subject information for user mapping.
    /// </para>
    /// <para>
    /// Supported by: Active Directory, OpenLDAP (with SASL EXTERNAL).
    /// </para>
    /// </remarks>
    Certificate = 20,

    #endregion

    #region SASL Modes (30-39)

    /// <summary>
    /// SASL PLAIN mechanism.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Simple Authentication and Security Layer with PLAIN mechanism.
    /// Sends credentials in clear text, should only be used over TLS.
    /// </para>
    /// <para>
    /// Supported by: OpenLDAP, Apple Directory.
    /// </para>
    /// </remarks>
    SaslPlain = 30,

    /// <summary>
    /// SASL EXTERNAL mechanism (certificate-based).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses external authentication (typically TLS client certificate).
    /// The identity is established by the TLS layer, not by LDAP credentials.
    /// </para>
    /// <para>
    /// Supported by: OpenLDAP, Apple Directory.
    /// </para>
    /// </remarks>
    SaslExternal = 31,

    /// <summary>
    /// SASL DIGEST-MD5 mechanism (legacy).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Challenge-response authentication using MD5 hash.
    /// Legacy mechanism, prefer SCRAM-SHA or Kerberos for new implementations.
    /// </para>
    /// <para>
    /// Supported by: OpenLDAP (deprecated).
    /// </para>
    /// </remarks>
    SaslDigestMd5 = 32,

    /// <summary>
    /// SASL GSSAPI mechanism (Kerberos).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses Generic Security Services API with Kerberos.
    /// Equivalent to <see cref="Kerberos"/> but using SASL framework.
    /// </para>
    /// <para>
    /// Supported by: OpenLDAP, Apple Directory.
    /// </para>
    /// </remarks>
    SaslGssapi = 33

    #endregion
}
