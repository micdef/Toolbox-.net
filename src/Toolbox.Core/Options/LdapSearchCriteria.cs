// @file LdapSearchCriteria.cs
// @brief Search criteria for LDAP user queries
// @details Provides a fluent API for building LDAP search filters
// @note Automatically translates to appropriate filter syntax per service

namespace Toolbox.Core.Options;

/// <summary>
/// Represents search criteria for LDAP user queries.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a service-agnostic way to specify search criteria.
/// Each LDAP service implementation translates these criteria to the
/// appropriate filter syntax (LDAP filter, OData, etc.).
/// </para>
/// <para>
/// Supports wildcard patterns using <c>*</c> for partial matching.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Search by display name pattern
/// var criteria = new LdapSearchCriteria
/// {
///     DisplayName = "John*",
///     Department = "Engineering"
/// };
/// var result = await ldapService.SearchUsersAsync(criteria, page: 1, pageSize: 25);
///
/// // Search by group membership
/// var criteria = new LdapSearchCriteria
/// {
///     MemberOfGroup = "CN=Developers,OU=Groups,DC=example,DC=com"
/// };
///
/// // Using fluent API
/// var criteria = LdapSearchCriteria.Create()
///     .WithDisplayName("*Smith")
///     .WithDepartment("Sales")
///     .EnabledOnly();
/// </code>
/// </example>
public sealed class LdapSearchCriteria
{
    /// <summary>
    /// Gets or sets the username pattern to match (supports * wildcard).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the display name pattern to match (supports * wildcard).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the first name pattern to match (supports * wildcard).
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name pattern to match (supports * wildcard).
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the email pattern to match (supports * wildcard).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the department to match.
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Gets or sets the job title pattern to match (supports * wildcard).
    /// </summary>
    public string? JobTitle { get; set; }

    /// <summary>
    /// Gets or sets the company to match.
    /// </summary>
    public string? Company { get; set; }

    /// <summary>
    /// Gets or sets the office location to match.
    /// </summary>
    public string? Office { get; set; }

    /// <summary>
    /// Gets or sets the city to match.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Gets or sets the country to match.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets the group DN or name that users must be members of.
    /// </summary>
    /// <remarks>
    /// For AD/LDAP: Use the full DN (e.g., "CN=Developers,OU=Groups,DC=example,DC=com").
    /// For Azure AD: Use the group ID or display name.
    /// </remarks>
    public string? MemberOfGroup { get; set; }

    /// <summary>
    /// Gets or sets a list of group DNs/names - users must be in at least one.
    /// </summary>
    public IList<string>? MemberOfAnyGroup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to return only enabled accounts.
    /// </summary>
    /// <value>
    /// <c>true</c> to return only enabled accounts;
    /// <c>false</c> to return only disabled accounts;
    /// <c>null</c> to return all accounts (default).
    /// </value>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets a custom LDAP filter to combine with other criteria.
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
    /// <returns>A new <see cref="LdapSearchCriteria"/> instance.</returns>
    public static LdapSearchCriteria Create() => new();

    /// <summary>
    /// Creates criteria to match all users.
    /// </summary>
    /// <returns>A criteria that matches all users.</returns>
    public static LdapSearchCriteria All() => new();

    /// <summary>
    /// Sets the username pattern.
    /// </summary>
    /// <param name="pattern">The username pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria WithUsername(string pattern)
    {
        Username = pattern;
        return this;
    }

    /// <summary>
    /// Sets the display name pattern.
    /// </summary>
    /// <param name="pattern">The display name pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria WithDisplayName(string pattern)
    {
        DisplayName = pattern;
        return this;
    }

    /// <summary>
    /// Sets the first name pattern.
    /// </summary>
    /// <param name="pattern">The first name pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria WithFirstName(string pattern)
    {
        FirstName = pattern;
        return this;
    }

    /// <summary>
    /// Sets the last name pattern.
    /// </summary>
    /// <param name="pattern">The last name pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria WithLastName(string pattern)
    {
        LastName = pattern;
        return this;
    }

    /// <summary>
    /// Sets the email pattern.
    /// </summary>
    /// <param name="pattern">The email pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria WithEmail(string pattern)
    {
        Email = pattern;
        return this;
    }

    /// <summary>
    /// Sets the department filter.
    /// </summary>
    /// <param name="department">The department name.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria WithDepartment(string department)
    {
        Department = department;
        return this;
    }

    /// <summary>
    /// Sets the job title pattern.
    /// </summary>
    /// <param name="pattern">The job title pattern (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria WithJobTitle(string pattern)
    {
        JobTitle = pattern;
        return this;
    }

    /// <summary>
    /// Sets the company filter.
    /// </summary>
    /// <param name="company">The company name.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria WithCompany(string company)
    {
        Company = company;
        return this;
    }

    /// <summary>
    /// Sets the group membership filter.
    /// </summary>
    /// <param name="groupDnOrName">The group DN or name.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria InGroup(string groupDnOrName)
    {
        MemberOfGroup = groupDnOrName;
        return this;
    }

    /// <summary>
    /// Sets the group membership filter for multiple groups (OR).
    /// </summary>
    /// <param name="groupDnsOrNames">The group DNs or names.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria InAnyGroup(params string[] groupDnsOrNames)
    {
        MemberOfAnyGroup = groupDnsOrNames.ToList();
        return this;
    }

    /// <summary>
    /// Filters to only enabled accounts.
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria EnabledOnly()
    {
        IsEnabled = true;
        return this;
    }

    /// <summary>
    /// Filters to only disabled accounts.
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria DisabledOnly()
    {
        IsEnabled = false;
        return this;
    }

    /// <summary>
    /// Adds a custom attribute filter.
    /// </summary>
    /// <param name="attributeName">The attribute name.</param>
    /// <param name="value">The value to match (supports * wildcard).</param>
    /// <returns>This instance for chaining.</returns>
    public LdapSearchCriteria WithAttribute(string attributeName, string value)
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
    public LdapSearchCriteria WithCustomFilter(string filter)
    {
        CustomFilter = filter;
        return this;
    }

    /// <summary>
    /// Gets a value indicating whether any criteria are specified.
    /// </summary>
    public bool HasCriteria =>
        !string.IsNullOrEmpty(Username) ||
        !string.IsNullOrEmpty(DisplayName) ||
        !string.IsNullOrEmpty(FirstName) ||
        !string.IsNullOrEmpty(LastName) ||
        !string.IsNullOrEmpty(Email) ||
        !string.IsNullOrEmpty(Department) ||
        !string.IsNullOrEmpty(JobTitle) ||
        !string.IsNullOrEmpty(Company) ||
        !string.IsNullOrEmpty(Office) ||
        !string.IsNullOrEmpty(City) ||
        !string.IsNullOrEmpty(Country) ||
        !string.IsNullOrEmpty(MemberOfGroup) ||
        MemberOfAnyGroup?.Count > 0 ||
        IsEnabled.HasValue ||
        !string.IsNullOrEmpty(CustomFilter) ||
        CustomAttributes?.Count > 0;
}
