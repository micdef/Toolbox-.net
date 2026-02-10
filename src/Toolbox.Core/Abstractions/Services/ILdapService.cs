// @file ILdapService.cs
// @brief Interface for LDAP directory services
// @details Defines contract for querying users, groups, and computers from directory services
// @note Supports Active Directory, Azure AD, OpenLDAP, and Apple Directory Services

using System.Security.Cryptography.X509Certificates;
using Toolbox.Core.Options;

namespace Toolbox.Core.Abstractions.Services;

/// <summary>
/// Defines the contract for LDAP directory services.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides methods for querying user, group, and computer information
/// from various directory services including:
/// </para>
/// <list type="bullet">
///   <item><description>Active Directory (Windows)</description></item>
///   <item><description>Azure Active Directory / Microsoft Entra ID</description></item>
///   <item><description>OpenLDAP (Linux)</description></item>
///   <item><description>Apple Directory Services (macOS)</description></item>
/// </list>
/// </remarks>
/// <seealso cref="IInstrumentedService"/>
/// <seealso cref="IAsyncDisposableService"/>
public interface ILdapService : IInstrumentedService, IAsyncDisposableService
{
    /// <summary>
    /// Gets a user by their username synchronously.
    /// </summary>
    /// <param name="username">The username to search for (sAMAccountName, uid, or userPrincipalName).</param>
    /// <returns>The user if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="username"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// var user = ldapService.GetUserByUsername("jdoe");
    /// if (user != null)
    /// {
    ///     Console.WriteLine($"Found: {user.DisplayName} ({user.Email})");
    /// }
    /// </code>
    /// </example>
    LdapUser? GetUserByUsername(string username);

    /// <summary>
    /// Gets a user by their username asynchronously.
    /// </summary>
    /// <param name="username">The username to search for (sAMAccountName, uid, or userPrincipalName).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the user if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="username"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <example>
    /// <code>
    /// var user = await ldapService.GetUserByUsernameAsync("jdoe");
    /// if (user != null)
    /// {
    ///     Console.WriteLine($"Found: {user.DisplayName} ({user.Email})");
    /// }
    /// </code>
    /// </example>
    Task<LdapUser?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their email address synchronously.
    /// </summary>
    /// <param name="email">The email address to search for.</param>
    /// <returns>The user if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="email"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    LdapUser? GetUserByEmail(string email);

    /// <summary>
    /// Gets a user by their email address asynchronously.
    /// </summary>
    /// <param name="email">The email address to search for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the user if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="email"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<LdapUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for users matching the specified filter.
    /// </summary>
    /// <param name="searchFilter">The search filter (LDAP filter syntax or provider-specific query).</param>
    /// <param name="maxResults">Maximum number of results to return. Default is 100.</param>
    /// <returns>A collection of matching users.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="searchFilter"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// // LDAP filter syntax for Active Directory/OpenLDAP
    /// var users = ldapService.SearchUsers("(department=Engineering)", maxResults: 50);
    ///
    /// // For Azure AD, use OData filter syntax
    /// var users = ldapService.SearchUsers("department eq 'Engineering'", maxResults: 50);
    /// </code>
    /// </example>
    IEnumerable<LdapUser> SearchUsers(string searchFilter, int maxResults = 100);

    /// <summary>
    /// Searches for users matching the specified filter asynchronously.
    /// </summary>
    /// <param name="searchFilter">The search filter (LDAP filter syntax or provider-specific query).</param>
    /// <param name="maxResults">Maximum number of results to return. Default is 100.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing a collection of matching users.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="searchFilter"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<IEnumerable<LdapUser>> SearchUsersAsync(string searchFilter, int maxResults = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates user credentials against the directory.
    /// </summary>
    /// <param name="username">The username to validate.</param>
    /// <param name="password">The password to validate.</param>
    /// <returns><c>true</c> if credentials are valid; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <remarks>
    /// <para>
    /// This method attempts to bind to the directory using the provided credentials.
    /// It does not cache or store the credentials.
    /// </para>
    /// <para>
    /// Note: Azure AD service does not support this method directly and will throw
    /// <see cref="NotSupportedException"/>. Use Azure AD authentication flows instead.
    /// </para>
    /// </remarks>
    bool ValidateCredentials(string username, string password);

    /// <summary>
    /// Validates user credentials against the directory asynchronously.
    /// </summary>
    /// <param name="username">The username to validate.</param>
    /// <param name="password">The password to validate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing <c>true</c> if credentials are valid; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the groups that a user belongs to.
    /// </summary>
    /// <param name="username">The username to get groups for.</param>
    /// <returns>A collection of group names or distinguished names.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="username"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    IEnumerable<string> GetUserGroups(string username);

    /// <summary>
    /// Gets the groups that a user belongs to asynchronously.
    /// </summary>
    /// <param name="username">The username to get groups for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing a collection of group names or distinguished names.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="username"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<IEnumerable<string>> GetUserGroupsAsync(string username, CancellationToken cancellationToken = default);

    #region Paginated Search Methods

    /// <summary>
    /// Gets all users with pagination.
    /// </summary>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of users per page. Default is 50.</param>
    /// <returns>A paged result containing users.</returns>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// var result = ldapService.GetAllUsers(page: 1, pageSize: 25);
    /// Console.WriteLine($"Found {result.TotalCount} users");
    /// foreach (var user in result.Items)
    /// {
    ///     Console.WriteLine(user.DisplayName);
    /// }
    /// </code>
    /// </example>
    PagedResult<LdapUser> GetAllUsers(int page = 1, int pageSize = 50);

    /// <summary>
    /// Gets all users with pagination asynchronously.
    /// </summary>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of users per page. Default is 50.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing a paged result of users.</returns>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<PagedResult<LdapUser>> GetAllUsersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for users matching the specified criteria with pagination.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of users per page. Default is 50.</param>
    /// <returns>A paged result containing matching users.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// // Search by department
    /// var criteria = new LdapSearchCriteria { Department = "Engineering" };
    /// var result = ldapService.SearchUsers(criteria, page: 1, pageSize: 25);
    ///
    /// // Search by name pattern
    /// var criteria = LdapSearchCriteria.Create()
    ///     .WithDisplayName("John*")
    ///     .EnabledOnly();
    /// var result = ldapService.SearchUsers(criteria);
    ///
    /// // Search by group membership
    /// var criteria = LdapSearchCriteria.Create()
    ///     .InGroup("CN=Developers,OU=Groups,DC=example,DC=com");
    /// var result = ldapService.SearchUsers(criteria);
    /// </code>
    /// </example>
    PagedResult<LdapUser> SearchUsers(LdapSearchCriteria criteria, int page = 1, int pageSize = 50);

    /// <summary>
    /// Searches for users matching the specified criteria with pagination asynchronously.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of users per page. Default is 50.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing a paged result of matching users.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <example>
    /// <code>
    /// var criteria = LdapSearchCriteria.Create()
    ///     .WithLastName("Smith")
    ///     .WithDepartment("Sales")
    ///     .EnabledOnly();
    ///
    /// var result = await ldapService.SearchUsersAsync(criteria, page: 1, pageSize: 25);
    ///
    /// while (result.HasNextPage)
    /// {
    ///     foreach (var user in result.Items)
    ///     {
    ///         Console.WriteLine($"{user.DisplayName} - {user.Email}");
    ///     }
    ///     result = await ldapService.SearchUsersAsync(criteria, result.Page + 1, pageSize: 25);
    /// }
    /// </code>
    /// </example>
    Task<PagedResult<LdapUser>> SearchUsersAsync(LdapSearchCriteria criteria, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets users who are members of the specified group with pagination.
    /// </summary>
    /// <param name="groupDnOrName">The group distinguished name or display name.</param>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of users per page. Default is 50.</param>
    /// <returns>A paged result containing group members.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="groupDnOrName"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    PagedResult<LdapUser> GetGroupMembers(string groupDnOrName, int page = 1, int pageSize = 50);

    /// <summary>
    /// Gets users who are members of the specified group with pagination asynchronously.
    /// </summary>
    /// <param name="groupDnOrName">The group distinguished name or display name.</param>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of users per page. Default is 50.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing a paged result of group members.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="groupDnOrName"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<PagedResult<LdapUser>> GetGroupMembersAsync(string groupDnOrName, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    #endregion

    #region Group Search Methods

    /// <summary>
    /// Gets a group by its name synchronously.
    /// </summary>
    /// <param name="groupName">The group name to search for (cn, displayName, or sAMAccountName).</param>
    /// <returns>The group if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="groupName"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// var group = ldapService.GetGroupByName("Developers");
    /// if (group != null)
    /// {
    ///     Console.WriteLine($"Found: {group.DisplayName} ({group.MemberCount} members)");
    /// }
    /// </code>
    /// </example>
    LdapGroup? GetGroupByName(string groupName);

    /// <summary>
    /// Gets a group by its name asynchronously.
    /// </summary>
    /// <param name="groupName">The group name to search for (cn, displayName, or sAMAccountName).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the group if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="groupName"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<LdapGroup?> GetGroupByNameAsync(string groupName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a group by its distinguished name or unique ID synchronously.
    /// </summary>
    /// <param name="distinguishedNameOrId">The group DN (for AD/LDAP) or ID (for Azure AD).</param>
    /// <returns>The group if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="distinguishedNameOrId"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    LdapGroup? GetGroupByDistinguishedName(string distinguishedNameOrId);

    /// <summary>
    /// Gets a group by its distinguished name or unique ID asynchronously.
    /// </summary>
    /// <param name="distinguishedNameOrId">The group DN (for AD/LDAP) or ID (for Azure AD).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the group if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="distinguishedNameOrId"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<LdapGroup?> GetGroupByDistinguishedNameAsync(string distinguishedNameOrId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for groups matching the specified filter.
    /// </summary>
    /// <param name="searchFilter">The search filter (LDAP filter syntax or provider-specific query).</param>
    /// <param name="maxResults">Maximum number of results to return. Default is 100.</param>
    /// <returns>A collection of matching groups.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="searchFilter"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// // LDAP filter syntax for Active Directory/OpenLDAP
    /// var groups = ldapService.SearchGroups("(cn=*Admin*)", maxResults: 50);
    ///
    /// // For Azure AD, use OData filter syntax
    /// var groups = ldapService.SearchGroups("startsWith(displayName, 'Admin')", maxResults: 50);
    /// </code>
    /// </example>
    IEnumerable<LdapGroup> SearchGroups(string searchFilter, int maxResults = 100);

    /// <summary>
    /// Searches for groups matching the specified filter asynchronously.
    /// </summary>
    /// <param name="searchFilter">The search filter (LDAP filter syntax or provider-specific query).</param>
    /// <param name="maxResults">Maximum number of results to return. Default is 100.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing a collection of matching groups.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="searchFilter"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<IEnumerable<LdapGroup>> SearchGroupsAsync(string searchFilter, int maxResults = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all groups with pagination.
    /// </summary>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of groups per page. Default is 50.</param>
    /// <returns>A paged result containing groups.</returns>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// var result = ldapService.GetAllGroups(page: 1, pageSize: 25);
    /// Console.WriteLine($"Found {result.TotalCount} groups");
    /// foreach (var group in result.Items)
    /// {
    ///     Console.WriteLine(group.DisplayName);
    /// }
    /// </code>
    /// </example>
    PagedResult<LdapGroup> GetAllGroups(int page = 1, int pageSize = 50);

    /// <summary>
    /// Gets all groups with pagination asynchronously.
    /// </summary>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of groups per page. Default is 50.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing a paged result of groups.</returns>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<PagedResult<LdapGroup>> GetAllGroupsAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for groups matching the specified criteria with pagination.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of groups per page. Default is 50.</param>
    /// <returns>A paged result containing matching groups.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// // Search by name pattern
    /// var criteria = new LdapGroupSearchCriteria { Name = "*Admins" };
    /// var result = ldapService.SearchGroups(criteria, page: 1, pageSize: 25);
    ///
    /// // Using fluent API
    /// var criteria = LdapGroupSearchCriteria.Create()
    ///     .WithName("Dev*")
    ///     .SecurityGroupsOnly();
    /// var result = ldapService.SearchGroups(criteria);
    /// </code>
    /// </example>
    PagedResult<LdapGroup> SearchGroups(LdapGroupSearchCriteria criteria, int page = 1, int pageSize = 50);

    /// <summary>
    /// Searches for groups matching the specified criteria with pagination asynchronously.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of groups per page. Default is 50.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing a paged result of matching groups.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <example>
    /// <code>
    /// var criteria = LdapGroupSearchCriteria.Create()
    ///     .WithName("*Developers*")
    ///     .SecurityGroupsOnly();
    ///
    /// var result = await ldapService.SearchGroupsAsync(criteria, page: 1, pageSize: 25);
    ///
    /// while (result.HasNextPage)
    /// {
    ///     foreach (var group in result.Items)
    ///     {
    ///         Console.WriteLine($"{group.DisplayName} - {group.MemberCount} members");
    ///     }
    ///     result = await ldapService.SearchGroupsAsync(criteria, result.Page + 1, pageSize: 25);
    /// }
    /// </code>
    /// </example>
    Task<PagedResult<LdapGroup>> SearchGroupsAsync(LdapGroupSearchCriteria criteria, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    #endregion

    #region Computer Search Methods

    /// <summary>
    /// Gets a computer by its name synchronously.
    /// </summary>
    /// <param name="computerName">The computer name to search for (cn, hostname, or sAMAccountName).</param>
    /// <returns>The computer if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="computerName"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// var computer = ldapService.GetComputerByName("SERVER01");
    /// if (computer != null)
    /// {
    ///     Console.WriteLine($"Found: {computer.DnsHostName} ({computer.OperatingSystem})");
    /// }
    /// </code>
    /// </example>
    LdapComputer? GetComputerByName(string computerName);

    /// <summary>
    /// Gets a computer by its name asynchronously.
    /// </summary>
    /// <param name="computerName">The computer name to search for (cn, hostname, or sAMAccountName).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the computer if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="computerName"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<LdapComputer?> GetComputerByNameAsync(string computerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a computer by its distinguished name or unique ID synchronously.
    /// </summary>
    /// <param name="distinguishedNameOrId">The computer DN (for AD/LDAP) or device ID (for Azure AD).</param>
    /// <returns>The computer if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="distinguishedNameOrId"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    LdapComputer? GetComputerByDistinguishedName(string distinguishedNameOrId);

    /// <summary>
    /// Gets a computer by its distinguished name or unique ID asynchronously.
    /// </summary>
    /// <param name="distinguishedNameOrId">The computer DN (for AD/LDAP) or device ID (for Azure AD).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the computer if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="distinguishedNameOrId"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<LdapComputer?> GetComputerByDistinguishedNameAsync(string distinguishedNameOrId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for computers matching the specified filter.
    /// </summary>
    /// <param name="searchFilter">The search filter (LDAP filter syntax or provider-specific query).</param>
    /// <param name="maxResults">Maximum number of results to return. Default is 100.</param>
    /// <returns>A collection of matching computers.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="searchFilter"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// // LDAP filter syntax for Active Directory
    /// var computers = ldapService.SearchComputers("(operatingSystem=Windows Server*)", maxResults: 50);
    ///
    /// // For Azure AD, use OData filter syntax
    /// var devices = ldapService.SearchComputers("startsWith(displayName, 'PC')", maxResults: 50);
    /// </code>
    /// </example>
    IEnumerable<LdapComputer> SearchComputers(string searchFilter, int maxResults = 100);

    /// <summary>
    /// Searches for computers matching the specified filter asynchronously.
    /// </summary>
    /// <param name="searchFilter">The search filter (LDAP filter syntax or provider-specific query).</param>
    /// <param name="maxResults">Maximum number of results to return. Default is 100.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing a collection of matching computers.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="searchFilter"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<IEnumerable<LdapComputer>> SearchComputersAsync(string searchFilter, int maxResults = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all computers with pagination.
    /// </summary>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of computers per page. Default is 50.</param>
    /// <returns>A paged result containing computers.</returns>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// var result = ldapService.GetAllComputers(page: 1, pageSize: 25);
    /// Console.WriteLine($"Found {result.TotalCount} computers");
    /// foreach (var computer in result.Items)
    /// {
    ///     Console.WriteLine($"{computer.Name} - {computer.OperatingSystem}");
    /// }
    /// </code>
    /// </example>
    PagedResult<LdapComputer> GetAllComputers(int page = 1, int pageSize = 50);

    /// <summary>
    /// Gets all computers with pagination asynchronously.
    /// </summary>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of computers per page. Default is 50.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing a paged result of computers.</returns>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<PagedResult<LdapComputer>> GetAllComputersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for computers matching the specified criteria with pagination.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of computers per page. Default is 50.</param>
    /// <returns>A paged result containing matching computers.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <example>
    /// <code>
    /// // Search by operating system
    /// var criteria = new LdapComputerSearchCriteria { OperatingSystem = "Windows Server*" };
    /// var result = ldapService.SearchComputers(criteria, page: 1, pageSize: 25);
    ///
    /// // Using fluent API
    /// var criteria = LdapComputerSearchCriteria.Create()
    ///     .WithName("SRV*")
    ///     .EnabledOnly();
    /// var result = ldapService.SearchComputers(criteria);
    /// </code>
    /// </example>
    PagedResult<LdapComputer> SearchComputers(LdapComputerSearchCriteria criteria, int page = 1, int pageSize = 50);

    /// <summary>
    /// Searches for computers matching the specified criteria with pagination asynchronously.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <param name="page">The page number (1-based). Default is 1.</param>
    /// <param name="pageSize">The number of computers per page. Default is 50.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing a paged result of matching computers.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when connection to directory fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <example>
    /// <code>
    /// var criteria = LdapComputerSearchCriteria.Create()
    ///     .WithOperatingSystem("Windows 10*")
    ///     .EnabledOnly();
    ///
    /// var result = await ldapService.SearchComputersAsync(criteria, page: 1, pageSize: 25);
    ///
    /// while (result.HasNextPage)
    /// {
    ///     foreach (var computer in result.Items)
    ///     {
    ///         Console.WriteLine($"{computer.Name} - {computer.OperatingSystem}");
    ///     }
    ///     result = await ldapService.SearchComputersAsync(criteria, result.Page + 1, pageSize: 25);
    /// }
    /// </code>
    /// </example>
    Task<PagedResult<LdapComputer>> SearchComputersAsync(LdapComputerSearchCriteria criteria, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    #endregion

    #region Advanced Authentication Methods

    /// <summary>
    /// Authenticates using the specified options synchronously.
    /// </summary>
    /// <param name="options">The authentication options.</param>
    /// <returns>The authentication result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when authentication options are invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown when the authentication mode is not supported.</exception>
    /// <example>
    /// <code>
    /// var options = new LdapAuthenticationOptions
    /// {
    ///     Mode = LdapAuthenticationMode.Simple,
    ///     Username = "jdoe",
    ///     Password = "password123"
    /// };
    /// var result = ldapService.Authenticate(options);
    /// if (result.IsAuthenticated)
    /// {
    ///     Console.WriteLine($"Welcome, {result.Username}!");
    /// }
    /// </code>
    /// </example>
    LdapAuthenticationResult Authenticate(LdapAuthenticationOptions options);

    /// <summary>
    /// Authenticates using the specified options asynchronously.
    /// </summary>
    /// <param name="options">The authentication options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the authentication result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when authentication options are invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown when the authentication mode is not supported.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <example>
    /// <code>
    /// var options = new LdapAuthenticationOptions
    /// {
    ///     Mode = LdapAuthenticationMode.Kerberos,
    ///     IncludeGroups = true,
    ///     IncludeClaims = true
    /// };
    /// var result = await ldapService.AuthenticateAsync(options);
    /// if (result.IsAuthenticated)
    /// {
    ///     Console.WriteLine($"Groups: {string.Join(", ", result.Groups ?? [])}");
    /// }
    /// </code>
    /// </example>
    Task<LdapAuthenticationResult> AuthenticateAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates with Kerberos using the current Windows security context.
    /// </summary>
    /// <param name="username">Optional username for explicit credentials. If null, uses current context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the authentication result.</returns>
    /// <exception cref="NotSupportedException">Thrown when Kerberos is not supported by this provider.</exception>
    /// <exception cref="InvalidOperationException">Thrown when Kerberos authentication fails.</exception>
    /// <remarks>
    /// <para>
    /// When <paramref name="username"/> is null, the method uses the current Windows
    /// security context (integrated Windows authentication).
    /// </para>
    /// <para>
    /// Supported by: Active Directory (primary), OpenLDAP with GSSAPI.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Use current Windows user
    /// var result = await ldapService.AuthenticateWithKerberosAsync();
    ///
    /// // Use specific user (requires password via AuthenticateAsync)
    /// var result = await ldapService.AuthenticateWithKerberosAsync("jdoe@DOMAIN.COM");
    /// </code>
    /// </example>
    Task<LdapAuthenticationResult> AuthenticateWithKerberosAsync(
        string? username = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates with a client certificate.
    /// </summary>
    /// <param name="certificate">The X.509 client certificate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the authentication result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="certificate"/> is <c>null</c>.</exception>
    /// <exception cref="NotSupportedException">Thrown when certificate authentication is not supported.</exception>
    /// <exception cref="InvalidOperationException">Thrown when certificate authentication fails.</exception>
    /// <remarks>
    /// <para>
    /// The certificate must contain a private key and be trusted by the directory server.
    /// User mapping depends on certificate subject or SAN fields.
    /// </para>
    /// <para>
    /// Supported by: Active Directory, OpenLDAP (SASL EXTERNAL).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using var cert = new X509Certificate2("user.pfx", "password");
    /// var result = await ldapService.AuthenticateWithCertificateAsync(cert);
    /// if (result.IsAuthenticated)
    /// {
    ///     Console.WriteLine($"Authenticated as: {result.Username}");
    /// }
    /// </code>
    /// </example>
    Task<LdapAuthenticationResult> AuthenticateWithCertificateAsync(
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the authentication modes supported by this provider.
    /// </summary>
    /// <returns>A read-only list of supported authentication modes.</returns>
    /// <remarks>
    /// <para>
    /// Use this method to check which authentication modes are available
    /// before attempting to authenticate.
    /// </para>
    /// <para>
    /// Typical supported modes by provider:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Active Directory: Simple, Kerberos, NTLM, Negotiate, IntegratedWindows, Certificate</description></item>
    ///   <item><description>Azure AD: Simple (via OAuth), Certificate</description></item>
    ///   <item><description>OpenLDAP: Simple, SaslPlain, SaslExternal, SaslGssapi, Certificate</description></item>
    ///   <item><description>Apple Directory: Simple, SaslPlain, Certificate</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var modes = ldapService.GetSupportedAuthenticationModes();
    /// if (modes.Contains(LdapAuthenticationMode.Kerberos))
    /// {
    ///     var result = await ldapService.AuthenticateWithKerberosAsync();
    /// }
    /// else
    /// {
    ///     // Fall back to simple authentication
    ///     var result = await ldapService.AuthenticateAsync(new LdapAuthenticationOptions
    ///     {
    ///         Mode = LdapAuthenticationMode.Simple,
    ///         Username = "user",
    ///         Password = "password"
    ///     });
    /// }
    /// </code>
    /// </example>
    IReadOnlyList<LdapAuthenticationMode> GetSupportedAuthenticationModes();

    #endregion
}
