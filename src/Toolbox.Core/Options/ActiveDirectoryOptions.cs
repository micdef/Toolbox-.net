// @file ActiveDirectoryOptions.cs
// @brief Configuration options for Active Directory service
// @details Settings for connecting to Windows Active Directory
// @note Supports both domain credentials and current Windows credentials

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for Windows Active Directory.
/// </summary>
/// <remarks>
/// <para>
/// These options configure how the Active Directory service connects
/// to a Windows domain controller. Supports multiple authentication modes:
/// </para>
/// <list type="bullet">
///   <item><description>Current Windows credentials (UseCurrentCredentials)</description></item>
///   <item><description>Explicit username/password</description></item>
///   <item><description>Anonymous bind (if server allows)</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var options = new ActiveDirectoryOptions
/// {
///     Domain = "corp.example.com",
///     UseSsl = true,
///     UseCurrentCredentials = true
/// };
/// </code>
/// </example>
public sealed class ActiveDirectoryOptions
{
    /// <summary>
    /// The configuration section name for binding from appsettings.json.
    /// </summary>
    public const string SectionName = "Toolbox:Ldap:ActiveDirectory";

    /// <summary>
    /// Gets or sets the domain name (e.g., "corp.example.com").
    /// </summary>
    /// <value>The fully qualified domain name.</value>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the domain controller server hostname or IP.
    /// </summary>
    /// <value>If empty, auto-discovers using DNS.</value>
    public string? Server { get; set; }

    /// <summary>
    /// Gets or sets the LDAP port.
    /// </summary>
    /// <value>Default is 389 (LDAP) or 636 (LDAPS).</value>
    public int Port { get; set; } = 389;

    /// <summary>
    /// Gets or sets the base DN for searches.
    /// </summary>
    /// <value>
    /// If not specified, derived from domain (e.g., "DC=corp,DC=example,DC=com").
    /// </value>
    public string? BaseDn { get; set; }

    /// <summary>
    /// Gets or sets the username for binding.
    /// </summary>
    /// <value>Format: "DOMAIN\user" or "user@domain.com".</value>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for binding.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets whether to use SSL/TLS (LDAPS).
    /// </summary>
    /// <value>Default is <c>false</c>. Set to <c>true</c> for port 636.</value>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Gets or sets whether to validate the server's SSL certificate.
    /// </summary>
    /// <value>Default is <c>true</c>. Set to <c>false</c> only for testing.</value>
    public bool ValidateCertificate { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use the current Windows credentials.
    /// </summary>
    /// <value>
    /// When <c>true</c>, ignores Username and Password and uses the
    /// credentials of the current Windows process.
    /// </value>
    public bool UseCurrentCredentials { get; set; }

    /// <summary>
    /// Gets or sets whether to follow LDAP referrals.
    /// </summary>
    /// <value>Default is <c>true</c>.</value>
    public bool FollowReferrals { get; set; } = true;

    /// <summary>
    /// Gets or sets the default user search filter template.
    /// </summary>
    /// <value>
    /// Use {0} as placeholder for the username.
    /// Default searches by sAMAccountName.
    /// </value>
    public string UserSearchFilter { get; set; } = "(&(objectClass=user)(objectCategory=person)(sAMAccountName={0}))";

    /// <summary>
    /// Gets or sets the email search filter template.
    /// </summary>
    /// <value>Use {0} as placeholder for the email address.</value>
    public string EmailSearchFilter { get; set; } = "(&(objectClass=user)(objectCategory=person)(mail={0}))";

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
