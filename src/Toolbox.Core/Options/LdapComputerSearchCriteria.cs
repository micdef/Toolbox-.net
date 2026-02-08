// @file LdapComputerSearchCriteria.cs
// @brief Search criteria for LDAP computer/device queries
// @details Provides a fluent API for building LDAP computer search filters
// @note Automatically translates to appropriate filter syntax per service

namespace Toolbox.Core.Options;

/// <summary>
/// Represents search criteria for LDAP computer/device queries.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a service-agnostic way to specify search criteria for computers.
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
/// var criteria = new LdapComputerSearchCriteria
/// {
///     Name = "SRV*",
///     OperatingSystem = "Windows Server*"
/// };
/// var result = await ldapService.SearchComputersAsync(criteria, page: 1, pageSize: 25);
///
/// // Using fluent API
/// var criteria = LdapComputerSearchCriteria.Create()
///     .WithName("PC*")
///     .EnabledOnly();
/// </code>
/// </example>
public sealed class LdapComputerSearchCriteria
{
    /// <summary>
    /// Gets or sets the computer name pattern to match (supports * wildcard).
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
    /// Gets or sets the DNS hostname pattern to match (supports * wildcard).
    /// </summary>
    public string? DnsHostName { get; set; }

    /// <summary>
    /// Gets or sets the operating system pattern to match (supports * wildcard).
    /// </summary>
    public string? OperatingSystem { get; set; }

    /// <summary>
    /// Gets or sets the operating system version pattern to match.
    /// </summary>
    public string? OperatingSystemVersion { get; set; }

    /// <summary>
    /// Gets or sets the location pattern to match (supports * wildcard).
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets whether to filter for enabled computers only.
    /// </summary>
    /// <value>
    /// <c>true</c> to return only enabled computers;
    /// <c>false</c> to return only disabled computers;
    /// <c>null</c> to return all computers (default).
    /// </value>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether to filter for managed devices only (Azure AD).
    /// </summary>
    public bool? IsManaged { get; set; }

    /// <summary>
    /// Gets or sets whether to filter for compliant devices only (Azure AD).
    /// </summary>
    public bool? IsCompliant { get; set; }

    /// <summary>
    /// Gets or sets the trust type to filter by (Azure AD).
    /// </summary>
    /// <remarks>
    /// Values: AzureAd, ServerAd, Workplace.
    /// </remarks>
    public string? TrustType { get; set; }

    /// <summary>
    /// Gets or sets the managed by user DN or ID to filter by.
    /// </summary>
    public string? ManagedBy { get; set; }

    /// <summary>
    /// Gets or sets a group DN or ID that computers must be members of.
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
    /// <returns>A new <see cref="LdapComputerSearchCriteria"/> instance.</returns>
    public static LdapComputerSearchCriteria Create() => new();

    /// <summary>
    /// Creates criteria to match all computers.
    /// </summary>
    /// <returns>A criteria that matches all computers.</returns>
    public static LdapComputerSearchCriteria All() => new();

    /// <summary>
    /// Sets the computer name pattern.
    /// </summary>
    /// <param name="pattern">The name pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria WithName(string pattern)
    {
        Name = pattern;
        return this;
    }

    /// <summary>
    /// Sets the display name pattern.
    /// </summary>
    /// <param name="pattern">The display name pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria WithDisplayName(string pattern)
    {
        DisplayName = pattern;
        return this;
    }

    /// <summary>
    /// Sets the description pattern.
    /// </summary>
    /// <param name="pattern">The description pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria WithDescription(string pattern)
    {
        Description = pattern;
        return this;
    }

    /// <summary>
    /// Sets the DNS hostname pattern.
    /// </summary>
    /// <param name="pattern">The DNS hostname pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria WithDnsHostName(string pattern)
    {
        DnsHostName = pattern;
        return this;
    }

    /// <summary>
    /// Sets the operating system pattern.
    /// </summary>
    /// <param name="pattern">The OS pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria WithOperatingSystem(string pattern)
    {
        OperatingSystem = pattern;
        return this;
    }

    /// <summary>
    /// Sets the operating system version pattern.
    /// </summary>
    /// <param name="pattern">The OS version pattern.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria WithOperatingSystemVersion(string pattern)
    {
        OperatingSystemVersion = pattern;
        return this;
    }

    /// <summary>
    /// Sets the location pattern.
    /// </summary>
    /// <param name="pattern">The location pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria WithLocation(string pattern)
    {
        Location = pattern;
        return this;
    }

    /// <summary>
    /// Filters to only enabled computers.
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria EnabledOnly()
    {
        IsEnabled = true;
        return this;
    }

    /// <summary>
    /// Filters to only disabled computers.
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria DisabledOnly()
    {
        IsEnabled = false;
        return this;
    }

    /// <summary>
    /// Filters to only managed devices (Azure AD).
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria ManagedOnly()
    {
        IsManaged = true;
        return this;
    }

    /// <summary>
    /// Filters to only compliant devices (Azure AD).
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria CompliantOnly()
    {
        IsCompliant = true;
        return this;
    }

    /// <summary>
    /// Filters by trust type (Azure AD).
    /// </summary>
    /// <param name="trustType">The trust type: AzureAd, ServerAd, or Workplace.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria WithTrustType(string trustType)
    {
        TrustType = trustType;
        return this;
    }

    /// <summary>
    /// Filters computers managed by a specific user or group.
    /// </summary>
    /// <param name="managerDnOrId">The manager's DN or ID.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria ManagedByUser(string managerDnOrId)
    {
        ManagedBy = managerDnOrId;
        return this;
    }

    /// <summary>
    /// Filters computers that are members of a specific group.
    /// </summary>
    /// <param name="groupDnOrName">The group DN or name.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapComputerSearchCriteria InGroup(string groupDnOrName)
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
    public LdapComputerSearchCriteria WithAttribute(string attributeName, string value)
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
    public LdapComputerSearchCriteria WithCustomFilter(string filter)
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
        !string.IsNullOrEmpty(DnsHostName) ||
        !string.IsNullOrEmpty(OperatingSystem) ||
        !string.IsNullOrEmpty(OperatingSystemVersion) ||
        !string.IsNullOrEmpty(Location) ||
        IsEnabled.HasValue ||
        IsManaged.HasValue ||
        IsCompliant.HasValue ||
        !string.IsNullOrEmpty(TrustType) ||
        !string.IsNullOrEmpty(ManagedBy) ||
        !string.IsNullOrEmpty(MemberOfGroup) ||
        !string.IsNullOrEmpty(CustomFilter) ||
        CustomAttributes?.Count > 0;
}
