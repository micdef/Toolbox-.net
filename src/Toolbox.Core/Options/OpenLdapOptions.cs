// @file OpenLdapOptions.cs
// @brief Configuration options for OpenLDAP service
// @details Settings for connecting to OpenLDAP or similar Linux directories
// @note Supports standard LDAP operations with configurable object classes

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for OpenLDAP or similar Linux directory services.
/// </summary>
/// <remarks>
/// <para>
/// These options configure how the service connects to an OpenLDAP server
/// or compatible LDAP directory (FreeIPA, 389 Directory Server, etc.).
/// </para>
/// <para>
/// Common object classes for users include:
/// </para>
/// <list type="bullet">
///   <item><description>inetOrgPerson (standard)</description></item>
///   <item><description>posixAccount (Linux/Unix)</description></item>
///   <item><description>organizationalPerson</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var options = new OpenLdapOptions
/// {
///     Host = "ldap.example.com",
///     Port = 389,
///     BaseDn = "dc=example,dc=com",
///     BindDn = "cn=admin,dc=example,dc=com",
///     BindPassword = "secret",
///     SecurityMode = LdapSecurityMode.StartTls
/// };
/// </code>
/// </example>
public sealed class OpenLdapOptions
{
    /// <summary>
    /// The configuration section name for binding from appsettings.json.
    /// </summary>
    public const string SectionName = "Toolbox:Ldap:OpenLdap";

    /// <summary>
    /// Gets or sets the LDAP server hostname or IP.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the LDAP port.
    /// </summary>
    /// <value>Default is 389 (LDAP) or 636 (LDAPS).</value>
    public int Port { get; set; } = 389;

    /// <summary>
    /// Gets or sets the base DN for searches.
    /// </summary>
    /// <value>Example: "dc=example,dc=com".</value>
    public string BaseDn { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bind DN for authentication.
    /// </summary>
    /// <value>Example: "cn=admin,dc=example,dc=com".</value>
    public string? BindDn { get; set; }

    /// <summary>
    /// Gets or sets the bind password.
    /// </summary>
    public string? BindPassword { get; set; }

    /// <summary>
    /// Gets or sets the security mode.
    /// </summary>
    /// <value>Default is <see cref="LdapSecurityMode.None"/>.</value>
    public LdapSecurityMode SecurityMode { get; set; } = LdapSecurityMode.None;

    /// <summary>
    /// Gets or sets whether to validate server certificates.
    /// </summary>
    /// <value>Default is <c>true</c>. Set to <c>false</c> only for testing.</value>
    public bool ValidateCertificate { get; set; } = true;

    /// <summary>
    /// Gets or sets the user object class.
    /// </summary>
    /// <value>Default is "inetOrgPerson".</value>
    public string UserObjectClass { get; set; } = "inetOrgPerson";

    /// <summary>
    /// Gets or sets the username attribute.
    /// </summary>
    /// <value>Default is "uid".</value>
    public string UsernameAttribute { get; set; } = "uid";

    /// <summary>
    /// Gets or sets the email attribute.
    /// </summary>
    /// <value>Default is "mail".</value>
    public string EmailAttribute { get; set; } = "mail";

    /// <summary>
    /// Gets or sets the display name attribute.
    /// </summary>
    /// <value>Default is "cn" (common name).</value>
    public string DisplayNameAttribute { get; set; } = "cn";

    /// <summary>
    /// Gets or sets the first name attribute.
    /// </summary>
    /// <value>Default is "givenName".</value>
    public string FirstNameAttribute { get; set; } = "givenName";

    /// <summary>
    /// Gets or sets the last name attribute.
    /// </summary>
    /// <value>Default is "sn" (surname).</value>
    public string LastNameAttribute { get; set; } = "sn";

    /// <summary>
    /// Gets or sets the group membership attribute (on user objects).
    /// </summary>
    /// <value>Default is "memberOf".</value>
    public string GroupMembershipAttribute { get; set; } = "memberOf";

    /// <summary>
    /// Gets or sets the group object class.
    /// </summary>
    /// <value>Default is "groupOfNames".</value>
    public string GroupObjectClass { get; set; } = "groupOfNames";

    /// <summary>
    /// Gets or sets the group member attribute (on group objects).
    /// </summary>
    /// <value>Default is "member".</value>
    public string GroupMemberAttribute { get; set; } = "member";

    /// <summary>
    /// Gets or sets the computer/device object class.
    /// </summary>
    /// <value>Default is "device". Can also be "ipHost" or custom class.</value>
    public string ComputerObjectClass { get; set; } = "device";

    /// <summary>
    /// Gets or sets the user search filter template.
    /// </summary>
    /// <value>Use {0} as placeholder for the username.</value>
    public string UserSearchFilter { get; set; } = "(&(objectClass=inetOrgPerson)(uid={0}))";

    /// <summary>
    /// Gets or sets the email search filter template.
    /// </summary>
    /// <value>Use {0} as placeholder for the email address.</value>
    public string EmailSearchFilter { get; set; } = "(&(objectClass=inetOrgPerson)(mail={0}))";

    /// <summary>
    /// Gets or sets the connection timeout.
    /// </summary>
    /// <value>Default is 30 seconds.</value>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the operation timeout.
    /// </summary>
    /// <value>Default is 60 seconds.</value>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets custom attribute names to retrieve.
    /// </summary>
    public IList<string> CustomAttributes { get; set; } = [];
}
