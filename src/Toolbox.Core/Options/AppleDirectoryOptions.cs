// @file AppleDirectoryOptions.cs
// @brief Configuration options for Apple Directory Services
// @details Settings for connecting to macOS Open Directory
// @note Apple Directory uses LDAP with Apple-specific object classes

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for Apple Directory Services (Open Directory).
/// </summary>
/// <remarks>
/// <para>
/// These options configure how the service connects to an Apple
/// Open Directory server (typically a macOS Server).
/// </para>
/// <para>
/// Apple Directory uses standard LDAP protocol with Apple-specific
/// object classes and attributes:
/// </para>
/// <list type="bullet">
///   <item><description>apple-user: Apple user accounts</description></item>
///   <item><description>apple-group: Apple group objects</description></item>
///   <item><description>apple-generateduid: Unique identifier</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var options = new AppleDirectoryOptions
/// {
///     Host = "od.example.com",
///     Port = 389,
///     BaseDn = "dc=example,dc=com",
///     BindDn = "uid=admin,cn=users,dc=example,dc=com",
///     BindPassword = "secret"
/// };
/// </code>
/// </example>
public sealed class AppleDirectoryOptions
{
    /// <summary>
    /// The configuration section name for binding from appsettings.json.
    /// </summary>
    public const string SectionName = "Toolbox:Ldap:AppleDirectory";

    /// <summary>
    /// Gets or sets the directory server hostname.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the LDAP port.
    /// </summary>
    /// <value>Default is 389 (LDAP) or 636 (LDAPS).</value>
    public int Port { get; set; } = 389;

    /// <summary>
    /// Gets or sets the base DN.
    /// </summary>
    /// <value>Example: "dc=example,dc=com".</value>
    public string BaseDn { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bind DN.
    /// </summary>
    /// <value>Example: "uid=admin,cn=users,dc=example,dc=com".</value>
    public string? BindDn { get; set; }

    /// <summary>
    /// Gets or sets the bind password.
    /// </summary>
    public string? BindPassword { get; set; }

    /// <summary>
    /// Gets or sets whether to use SSL.
    /// </summary>
    /// <value>Default is <c>false</c>. Set to <c>true</c> for port 636.</value>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Gets or sets whether to validate server certificates.
    /// </summary>
    /// <value>Default is <c>true</c>. Set to <c>false</c> only for testing.</value>
    public bool ValidateCertificate { get; set; } = true;

    /// <summary>
    /// Gets or sets the user object class for Apple Directory.
    /// </summary>
    /// <value>Default is "apple-user".</value>
    public string UserObjectClass { get; set; } = "apple-user";

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
    /// Gets or sets the unique identifier attribute.
    /// </summary>
    /// <value>Default is "apple-generateduid".</value>
    public string UniqueIdAttribute { get; set; } = "apple-generateduid";

    /// <summary>
    /// Gets or sets the group membership attribute.
    /// </summary>
    /// <value>Default is "memberOf".</value>
    public string GroupMembershipAttribute { get; set; } = "memberOf";

    /// <summary>
    /// Gets or sets the user search filter.
    /// </summary>
    /// <value>Use {0} as placeholder for the username.</value>
    public string UserSearchFilter { get; set; } = "(&(objectClass=apple-user)(uid={0}))";

    /// <summary>
    /// Gets or sets the email search filter.
    /// </summary>
    /// <value>Use {0} as placeholder for the email address.</value>
    public string EmailSearchFilter { get; set; } = "(&(objectClass=apple-user)(mail={0}))";

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
