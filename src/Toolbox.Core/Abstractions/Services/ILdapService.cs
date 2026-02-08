// @file ILdapService.cs
// @brief Interface for LDAP directory services
// @details Defines contract for querying users from directory services
// @note Supports Active Directory, Azure AD, OpenLDAP, and Apple Directory Services

using Toolbox.Core.Options;

namespace Toolbox.Core.Abstractions.Services;

/// <summary>
/// Defines the contract for LDAP directory services.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides methods for querying user information
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
}
