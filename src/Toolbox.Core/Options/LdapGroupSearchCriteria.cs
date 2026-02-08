// @file LdapGroupSearchCriteria.cs
// @brief Search criteria for LDAP group queries
// @details Provides a fluent API for building LDAP group search filters
// @note Automatically translates to appropriate filter syntax per service

namespace Toolbox.Core.Options;

/// <summary>
/// Represents search criteria for LDAP group queries.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a service-agnostic way to specify search criteria for groups.
/// Each LDAP service implementation translates these criteria to the
/// appropriate filter syntax (LDAP filter, OData, etc.).
/// </para>
/// <para>
/// Supports wildcard patterns using <c>*</c> for partial matching.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Search by name pattern
/// var criteria = new LdapGroupSearchCriteria
/// {
///     Name = "Dev*",
///     IsSecurityGroup = true
/// };
/// var result = await ldapService.SearchGroupsAsync(criteria, page: 1, pageSize: 25);
///
/// // Using fluent API
/// var criteria = LdapGroupSearchCriteria.Create()
///     .WithName("*Admins")
///     .SecurityGroupsOnly();
/// </code>
/// </example>
public sealed class LdapGroupSearchCriteria
{
    /// <summary>
    /// Gets or sets the group name pattern to match (supports * wildcard).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the display name pattern to match (supports * wildcard).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description pattern to match (supports * wildcard).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the email pattern to match (supports * wildcard).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets whether to filter for security groups only.
    /// </summary>
    /// <value>
    /// <c>true</c> to return only security groups;
    /// <c>false</c> to return only distribution/non-security groups;
    /// <c>null</c> to return all groups (default).
    /// </value>
    public bool? IsSecurityGroup { get; set; }

    /// <summary>
    /// Gets or sets whether to filter for mail-enabled groups only.
    /// </summary>
    /// <value>
    /// <c>true</c> to return only mail-enabled groups;
    /// <c>false</c> to return only non-mail-enabled groups;
    /// <c>null</c> to return all groups (default).
    /// </value>
    public bool? IsMailEnabled { get; set; }

    /// <summary>
    /// Gets or sets the group scope to filter by.
    /// </summary>
    /// <remarks>
    /// For Active Directory: DomainLocal, Global, or Universal.
    /// Not applicable for other directory types.
    /// </remarks>
    public string? GroupScope { get; set; }

    /// <summary>
    /// Gets or sets the managed by user DN or ID to filter by.
    /// </summary>
    public string? ManagedBy { get; set; }

    /// <summary>
    /// Gets or sets a member DN or ID that groups must contain.
    /// </summary>
    /// <remarks>
    /// Returns groups that have this user/group as a direct member.
    /// </remarks>
    public string? HasMember { get; set; }

    /// <summary>
    /// Gets or sets a parent group DN or ID that groups must be members of.
    /// </summary>
    public string? MemberOfGroup { get; set; }

    /// <summary>
    /// Gets or sets a custom filter to combine with other criteria.
    /// </summary>
    /// <remarks>
    /// For AD/OpenLDAP: Standard LDAP filter syntax.
    /// For Azure AD: OData filter syntax.
    /// This filter is AND-combined with other criteria.
    /// </remarks>
    public string? CustomFilter { get; set; }

    /// <summary>
    /// Gets or sets custom attribute filters.
    /// </summary>
    /// <remarks>
    /// Key: attribute name, Value: value to match (supports * wildcard).
    /// </remarks>
    public IDictionary<string, string>? CustomAttributes { get; set; }

    /// <summary>
    /// Creates a new empty search criteria instance.
    /// </summary>
    /// <returns>A new <see cref="LdapGroupSearchCriteria"/> instance.</returns>
    public static LdapGroupSearchCriteria Create() => new();

    /// <summary>
    /// Creates criteria to match all groups.
    /// </summary>
    /// <returns>A criteria that matches all groups.</returns>
    public static LdapGroupSearchCriteria All() => new();

    /// <summary>
    /// Sets the group name pattern.
    /// </summary>
    /// <param name="pattern">The name pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria WithName(string pattern)
    {
        Name = pattern;
        return this;
    }

    /// <summary>
    /// Sets the display name pattern.
    /// </summary>
    /// <param name="pattern">The display name pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria WithDisplayName(string pattern)
    {
        DisplayName = pattern;
        return this;
    }

    /// <summary>
    /// Sets the description pattern.
    /// </summary>
    /// <param name="pattern">The description pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria WithDescription(string pattern)
    {
        Description = pattern;
        return this;
    }

    /// <summary>
    /// Sets the email pattern.
    /// </summary>
    /// <param name="pattern">The email pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria WithEmail(string pattern)
    {
        Email = pattern;
        return this;
    }

    /// <summary>
    /// Filters to only security groups.
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria SecurityGroupsOnly()
    {
        IsSecurityGroup = true;
        return this;
    }

    /// <summary>
    /// Filters to only distribution groups (non-security).
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria DistributionGroupsOnly()
    {
        IsSecurityGroup = false;
        return this;
    }

    /// <summary>
    /// Filters to only mail-enabled groups.
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria MailEnabledOnly()
    {
        IsMailEnabled = true;
        return this;
    }

    /// <summary>
    /// Sets the group scope filter (AD only).
    /// </summary>
    /// <param name="scope">The scope: DomainLocal, Global, or Universal.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria WithScope(string scope)
    {
        GroupScope = scope;
        return this;
    }

    /// <summary>
    /// Filters groups managed by a specific user or group.
    /// </summary>
    /// <param name="managerDnOrId">The manager's DN or ID.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria ManagedByUser(string managerDnOrId)
    {
        ManagedBy = managerDnOrId;
        return this;
    }

    /// <summary>
    /// Filters groups that contain a specific member.
    /// </summary>
    /// <param name="memberDnOrId">The member's DN or ID.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria ContainingMember(string memberDnOrId)
    {
        HasMember = memberDnOrId;
        return this;
    }

    /// <summary>
    /// Filters groups that are members of a specific parent group.
    /// </summary>
    /// <param name="groupDnOrName">The parent group DN or name.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria InGroup(string groupDnOrName)
    {
        MemberOfGroup = groupDnOrName;
        return this;
    }

    /// <summary>
    /// Adds a custom attribute filter.
    /// </summary>
    /// <param name="attributeName">The attribute name.</param>
    /// <param name="value">The value to match (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria WithAttribute(string attributeName, string value)
    {
        CustomAttributes ??= new Dictionary<string, string>();
        CustomAttributes[attributeName] = value;
        return this;
    }

    /// <summary>
    /// Sets a custom filter to combine with other criteria.
    /// </summary>
    /// <param name="filter">The custom filter (LDAP or OData syntax).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupSearchCriteria WithCustomFilter(string filter)
    {
        CustomFilter = filter;
        return this;
    }

    /// <summary>
    /// Gets a value indicating whether any criteria are specified.
    /// </summary>
    public bool HasCriteria =>
        !string.IsNullOrEmpty(Name) ||
        !string.IsNullOrEmpty(DisplayName) ||
        !string.IsNullOrEmpty(Description) ||
        !string.IsNullOrEmpty(Email) ||
        IsSecurityGroup.HasValue ||
        IsMailEnabled.HasValue ||
        !string.IsNullOrEmpty(GroupScope) ||
        !string.IsNullOrEmpty(ManagedBy) ||
        !string.IsNullOrEmpty(HasMember) ||
        !string.IsNullOrEmpty(MemberOfGroup) ||
        !string.IsNullOrEmpty(CustomFilter) ||
        CustomAttributes?.Count > 0;
}
