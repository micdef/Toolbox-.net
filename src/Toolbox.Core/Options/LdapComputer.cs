// @file LdapComputer.cs
// @brief Data model for LDAP computer/device information
// @details Contains common computer attributes across directory services
// @note Properties may be null depending on directory configuration

namespace Toolbox.Core.Options;

/// <summary>
/// Represents a computer or device retrieved from a directory service.
/// </summary>
/// <remarks>
/// <para>
/// This class contains common computer/device attributes that are typically available
/// across different directory services. Not all properties may be populated
/// depending on the directory configuration and permissions.
/// </para>
/// <para>
/// Supported directory types:
/// </para>
/// <list type="bullet">
///   <item><description>Active Directory (Windows) - Computer objects</description></item>
///   <item><description>Azure Active Directory / Microsoft Entra ID - Device objects</description></item>
///   <item><description>OpenLDAP (Linux) - Device or ipHost objects</description></item>
///   <item><description>Apple Directory Services (macOS) - Computer objects</description></item>
/// </list>
/// </remarks>
public sealed class LdapComputer
{
    /// <summary>
    /// Gets or sets the unique identifier of the computer.
    /// </summary>
    /// <value>
    /// For AD: objectGUID, For Azure AD: deviceId, For OpenLDAP: entryUUID.
    /// </value>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the computer name (cn or hostname).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the distinguished name (DN) of the computer.
    /// </summary>
    /// <value>The full LDAP path to the computer object.</value>
    public string? DistinguishedName { get; set; }

    /// <summary>
    /// Gets or sets the computer's display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the computer's description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the DNS hostname.
    /// </summary>
    public string? DnsHostName { get; set; }

    /// <summary>
    /// Gets or sets the SAM account name (AD).
    /// </summary>
    /// <value>Usually the computer name with a trailing $.</value>
    public string? SamAccountName { get; set; }

    /// <summary>
    /// Gets or sets the operating system name.
    /// </summary>
    public string? OperatingSystem { get; set; }

    /// <summary>
    /// Gets or sets the operating system version.
    /// </summary>
    public string? OperatingSystemVersion { get; set; }

    /// <summary>
    /// Gets or sets the operating system service pack.
    /// </summary>
    public string? OperatingSystemServicePack { get; set; }

    /// <summary>
    /// Gets or sets the IP addresses of the computer.
    /// </summary>
    public IList<string> IpAddresses { get; set; } = [];

    /// <summary>
    /// Gets or sets the MAC addresses of the computer.
    /// </summary>
    public IList<string> MacAddresses { get; set; } = [];

    /// <summary>
    /// Gets or sets the location of the computer.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the managed by user or group (DN or ID).
    /// </summary>
    public string? ManagedBy { get; set; }

    /// <summary>
    /// Gets or sets the organizational unit where the computer resides.
    /// </summary>
    public string? OrganizationalUnit { get; set; }

    /// <summary>
    /// Gets or sets whether the computer account is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether the device is managed (Azure AD).
    /// </summary>
    public bool? IsManaged { get; set; }

    /// <summary>
    /// Gets or sets whether the device is compliant (Azure AD).
    /// </summary>
    public bool? IsCompliant { get; set; }

    /// <summary>
    /// Gets or sets the trust type (Azure AD: AzureAd, ServerAd, Workplace).
    /// </summary>
    public string? TrustType { get; set; }

    /// <summary>
    /// Gets or sets when the computer was created.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the computer was last modified.
    /// </summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets when the computer last logged on.
    /// </summary>
    public DateTimeOffset? LastLogon { get; set; }

    /// <summary>
    /// Gets or sets when the password was last set.
    /// </summary>
    public DateTimeOffset? PasswordLastSet { get; set; }

    /// <summary>
    /// Gets or sets the approximate last logon timestamp (AD replicated).
    /// </summary>
    public DateTimeOffset? LastLogonTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the groups the computer belongs to.
    /// </summary>
    public IList<string> MemberOf { get; set; } = [];

    /// <summary>
    /// Gets or sets the service principal names (AD).
    /// </summary>
    public IList<string> ServicePrincipalNames { get; set; } = [];

    /// <summary>
    /// Gets or sets additional custom attributes.
    /// </summary>
    /// <value>A dictionary of attribute names and values.</value>
    public IDictionary<string, object?> CustomAttributes { get; set; } = new Dictionary<string, object?>();

    /// <summary>
    /// Gets or sets the source directory type.
    /// </summary>
    public LdapDirectoryType DirectoryType { get; set; }
}
