// @file LdapUser.cs
// @brief Data model for LDAP user information
// @details Contains common user attributes across directory services
// @note Properties may be null depending on directory configuration

namespace Toolbox.Core.Options;

/// <summary>
/// Represents a user retrieved from a directory service.
/// </summary>
/// <remarks>
/// <para>
/// This class contains common user attributes that are typically available
/// across different directory services. Not all properties may be populated
/// depending on the directory configuration and permissions.
/// </para>
/// <para>
/// Supported directory types:
/// </para>
/// <list type="bullet">
///   <item><description>Active Directory (Windows)</description></item>
///   <item><description>Azure Active Directory / Microsoft Entra ID</description></item>
///   <item><description>OpenLDAP (Linux)</description></item>
///   <item><description>Apple Directory Services (macOS)</description></item>
/// </list>
/// </remarks>
public sealed class LdapUser
{
    /// <summary>
    /// Gets or sets the unique identifier of the user.
    /// </summary>
    /// <value>
    /// For AD: objectGUID, For Azure AD: id, For OpenLDAP: entryUUID, For Apple: apple-generateduid.
    /// </value>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the username (sAMAccountName in AD, uid in LDAP).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user principal name (UPN).
    /// </summary>
    /// <value>Usually in the format username@domain.</value>
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Gets or sets the distinguished name (DN) of the user.
    /// </summary>
    /// <value>The full LDAP path to the user object.</value>
    public string? DistinguishedName { get; set; }

    /// <summary>
    /// Gets or sets the user's display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the user's first name (given name).
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the user's last name (surname).
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the user's phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the user's mobile phone number.
    /// </summary>
    public string? MobilePhone { get; set; }

    /// <summary>
    /// Gets or sets the user's job title.
    /// </summary>
    public string? JobTitle { get; set; }

    /// <summary>
    /// Gets or sets the user's department.
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Gets or sets the user's company.
    /// </summary>
    public string? Company { get; set; }

    /// <summary>
    /// Gets or sets the user's office location.
    /// </summary>
    public string? Office { get; set; }

    /// <summary>
    /// Gets or sets the user's manager distinguished name or ID.
    /// </summary>
    public string? Manager { get; set; }

    /// <summary>
    /// Gets or sets the user's street address.
    /// </summary>
    public string? StreetAddress { get; set; }

    /// <summary>
    /// Gets or sets the user's city.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Gets or sets the user's state or province.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets the user's postal code.
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the user's country.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets whether the account is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether the account is locked out.
    /// </summary>
    public bool? IsLockedOut { get; set; }

    /// <summary>
    /// Gets or sets when the account was created.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the account was last modified.
    /// </summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets when the user last logged in.
    /// </summary>
    public DateTimeOffset? LastLogon { get; set; }

    /// <summary>
    /// Gets or sets when the password expires.
    /// </summary>
    public DateTimeOffset? PasswordExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the groups the user belongs to.
    /// </summary>
    public IList<string> Groups { get; set; } = [];

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

/// <summary>
/// Specifies the type of directory service.
/// </summary>
public enum LdapDirectoryType
{
    /// <summary>
    /// Active Directory on Windows.
    /// </summary>
    ActiveDirectory = 0,

    /// <summary>
    /// Azure Active Directory / Microsoft Entra ID.
    /// </summary>
    AzureActiveDirectory = 1,

    /// <summary>
    /// OpenLDAP or similar Linux directory.
    /// </summary>
    OpenLdap = 2,

    /// <summary>
    /// Apple Directory Services (Open Directory).
    /// </summary>
    AppleDirectory = 3
}
