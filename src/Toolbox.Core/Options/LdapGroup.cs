// @file LdapGroup.cs
// @brief Data model for LDAP group information
// @details Contains common group attributes across directory services
// @note Properties may be null depending on directory configuration

namespace Toolbox.Core.Options;

/// <summary>
/// Represents a group retrieved from a directory service.
/// </summary>
/// <remarks>
/// <para>
/// This class contains common group attributes that are typically available
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
public sealed class LdapGroup
{
    /// <summary>
    /// Gets or sets the unique identifier of the group.
    /// </summary>
    /// <value>
    /// For AD: objectGUID, For Azure AD: id, For OpenLDAP: entryUUID, For Apple: apple-generateduid.
    /// </value>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the group name (cn in LDAP, displayName in Azure AD).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the distinguished name (DN) of the group.
    /// </summary>
    /// <value>The full LDAP path to the group object.</value>
    public string? DistinguishedName { get; set; }

    /// <summary>
    /// Gets or sets the group's display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the group's description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the group's email address (for mail-enabled groups).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the group type.
    /// </summary>
    /// <value>
    /// For AD: Security or Distribution.
    /// For Azure AD: Unified, Security, or DynamicMembership.
    /// </value>
    public string? GroupType { get; set; }

    /// <summary>
    /// Gets or sets the group scope.
    /// </summary>
    /// <value>
    /// For AD: DomainLocal, Global, or Universal.
    /// </value>
    public string? GroupScope { get; set; }

    /// <summary>
    /// Gets or sets whether the group is a security group.
    /// </summary>
    public bool? IsSecurityGroup { get; set; }

    /// <summary>
    /// Gets or sets whether the group is mail-enabled.
    /// </summary>
    public bool? IsMailEnabled { get; set; }

    /// <summary>
    /// Gets or sets the managed by user or group (DN or ID).
    /// </summary>
    public string? ManagedBy { get; set; }

    /// <summary>
    /// Gets or sets the organizational unit where the group resides.
    /// </summary>
    public string? OrganizationalUnit { get; set; }

    /// <summary>
    /// Gets or sets when the group was created.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the group was last modified.
    /// </summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of members in the group.
    /// </summary>
    /// <remarks>
    /// This value may not always be available depending on the directory service
    /// and query configuration.
    /// </remarks>
    public int? MemberCount { get; set; }

    /// <summary>
    /// Gets or sets the direct members of the group (user/group DNs or IDs).
    /// </summary>
    /// <remarks>
    /// This list contains only direct members. Nested group members are not included.
    /// For large groups, this list may be empty and you should use the
    /// ILdapService.GetGroupMembersAsync method for paginated member retrieval.
    /// </remarks>
    public IList<string> Members { get; set; } = [];

    /// <summary>
    /// Gets or sets the groups that this group is a member of.
    /// </summary>
    public IList<string> MemberOf { get; set; } = [];

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
