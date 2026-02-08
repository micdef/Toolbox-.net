// @file LdapSecurityMode.cs
// @brief Enumeration of LDAP connection security modes
// @details Defines the available security modes for LDAP connections
// @note Used by OpenLDAP and Apple Directory services

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the security mode for LDAP connections.
/// </summary>
/// <remarks>
/// <para>
/// This enumeration defines how the connection to an LDAP server
/// should be secured:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="None"/>: No encryption (port 389)</description></item>
///   <item><description><see cref="Ssl"/>: SSL/TLS on connect (port 636)</description></item>
///   <item><description><see cref="StartTls"/>: STARTTLS upgrade after connect (port 389)</description></item>
/// </list>
/// </remarks>
public enum LdapSecurityMode
{
    /// <summary>
    /// No encryption. Uses standard LDAP port 389.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not recommended for production use as credentials and data
    /// are transmitted in plain text.
    /// </para>
    /// </remarks>
    None = 0,

    /// <summary>
    /// SSL/TLS encryption on connect (LDAPS). Uses port 636.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The connection is encrypted from the start using SSL/TLS.
    /// This is the traditional secure LDAP method.
    /// </para>
    /// </remarks>
    Ssl = 1,

    /// <summary>
    /// STARTTLS upgrade after initial connection. Uses port 389.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The connection starts unencrypted on port 389, then upgrades
    /// to TLS using the STARTTLS command. This is the modern
    /// recommended approach for secure LDAP connections.
    /// </para>
    /// </remarks>
    StartTls = 2
}
