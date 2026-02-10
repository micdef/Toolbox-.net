// @file AzureAdService.cs
// @brief Azure AD service implementation
// @details Implements ILdapService using Microsoft Graph API
// @note Azure AD does not support direct LDAP; uses Graph API instead

using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Ldap;

/// <summary>
/// LDAP service implementation for Azure Active Directory using Microsoft Graph.
/// </summary>
/// <remarks>
/// <para>
/// Azure AD (now Microsoft Entra ID) does not support direct LDAP connections.
/// This service uses the Microsoft Graph API to provide equivalent functionality.
/// </para>
/// <para>
/// Features:
/// </para>
/// <list type="bullet">
///   <item><description>User lookup by username (UPN, mailNickname) or email</description></item>
///   <item><description>OData filter-based search</description></item>
///   <item><description>Group membership retrieval</description></item>
///   <item><description>Client secret, certificate, or managed identity authentication</description></item>
/// </list>
/// <para>
/// Note: <see cref="ValidateCredentials"/> is not supported as Azure AD
/// requires OAuth2/OIDC flows for authentication. Use Azure AD B2C or
/// MSAL for user authentication.
/// </para>
/// </remarks>
/// <seealso cref="ILdapService"/>
public sealed class AzureAdService : BaseAsyncDisposableService, ILdapService
{
    private readonly AzureAdOptions _options;
    private readonly ILogger<AzureAdService> _logger;
    private readonly Lazy<GraphServiceClient> _graphClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAdService"/> class.
    /// </summary>
    /// <param name="options">The Azure AD options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when required settings are empty.</exception>
    public AzureAdService(
        IOptions<AzureAdOptions> options,
        ILogger<AzureAdService> logger)
        : base("AzureAdService", logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.TenantId))
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new ArgumentException("ClientId cannot be empty.", nameof(options));
        }

        _graphClient = new Lazy<GraphServiceClient>(CreateGraphClient);

        _logger.LogDebug(
            "AzureAdService initialized for tenant {TenantId} with auth mode {AuthMode}",
            _options.TenantId,
            _options.AuthenticationMode);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAdService"/> class.
    /// </summary>
    /// <param name="options">The Azure AD options.</param>
    /// <param name="logger">The logger instance.</param>
    public AzureAdService(
        AzureAdOptions options,
        ILogger<AzureAdService> logger)
        : this(Microsoft.Extensions.Options.Options.Create(options), logger)
    {
    }

    /// <inheritdoc />
    public LdapUser? GetUserByUsername(string username)
    {
        return GetUserByUsernameAsync(username).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapUser?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(username);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Search by userPrincipalName or mailNickname
            var escapedUsername = EscapeODataFilter(username);
            var filter = $"userPrincipalName eq '{escapedUsername}' or mailNickname eq '{escapedUsername}'";

            var users = await _graphClient.Value.Users
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = filter;
                    config.QueryParameters.Select = _options.SelectProperties.ToArray();
                    config.QueryParameters.Top = 1;
                }, cancellationToken);

            var graphUser = users?.Value?.FirstOrDefault();
            if (graphUser == null)
            {
                _logger.LogDebug("User not found in Azure AD: {Username}", username);
                RecordOperation("GetUserByUsername", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetUserByUsername", "user", 0, sw.ElapsedMilliseconds, true);
                return null;
            }

            var user = MapToLdapUser(graphUser);
            RecordOperation("GetUserByUsername", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetUserByUsername", "user", 1, sw.ElapsedMilliseconds, true);

            return user;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetUserByUsername", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching for user: {Username}", username);
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public LdapUser? GetUserByEmail(string email)
    {
        return GetUserByEmailAsync(email).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(email);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var escapedEmail = EscapeODataFilter(email);
            var filter = $"mail eq '{escapedEmail}'";

            var users = await _graphClient.Value.Users
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = filter;
                    config.QueryParameters.Select = _options.SelectProperties.ToArray();
                    config.QueryParameters.Top = 1;
                }, cancellationToken);

            var graphUser = users?.Value?.FirstOrDefault();
            if (graphUser == null)
            {
                _logger.LogDebug("User not found by email in Azure AD: {Email}", email);
                RecordOperation("GetUserByEmail", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetUserByEmail", "user", 0, sw.ElapsedMilliseconds, true);
                return null;
            }

            var user = MapToLdapUser(graphUser);
            RecordOperation("GetUserByEmail", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetUserByEmail", "user", 1, sw.ElapsedMilliseconds, true);

            return user;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetUserByEmail", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching for user by email: {Email}", email);
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public IEnumerable<LdapUser> SearchUsers(string searchFilter, int maxResults = 100)
    {
        return SearchUsersAsync(searchFilter, maxResults).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LdapUser>> SearchUsersAsync(string searchFilter, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(searchFilter);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var users = await _graphClient.Value.Users
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = searchFilter;
                    config.QueryParameters.Select = _options.SelectProperties.ToArray();
                    config.QueryParameters.Top = maxResults;
                }, cancellationToken);

            var result = (users?.Value ?? [])
                .Select(MapToLdapUser)
                .ToList();

            RecordOperation("SearchUsers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "SearchUsers", "user", result.Count, sw.ElapsedMilliseconds, true);

            return result;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchUsers", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching users with filter: {Filter}", searchFilter);
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method is not supported for Azure AD as it requires OAuth2/OIDC flows.
    /// </remarks>
    /// <exception cref="NotSupportedException">Always thrown as Azure AD requires OAuth2 flows.</exception>
    public bool ValidateCredentials(string username, string password)
    {
        throw new NotSupportedException(
            "Azure AD does not support direct credential validation. " +
            "Use Azure AD B2C, MSAL, or OAuth2/OIDC flows for user authentication.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method is not supported for Azure AD as it requires OAuth2/OIDC flows.
    /// </remarks>
    /// <exception cref="NotSupportedException">Always thrown as Azure AD requires OAuth2 flows.</exception>
    public Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Azure AD does not support direct credential validation. " +
            "Use Azure AD B2C, MSAL, or OAuth2/OIDC flows for user authentication.");
    }

    /// <inheritdoc />
    public IEnumerable<string> GetUserGroups(string username)
    {
        return GetUserGroupsAsync(username).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetUserGroupsAsync(string username, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(username);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // First find the user
            var user = await GetUserByUsernameAsync(username, cancellationToken);
            if (user?.Id == null)
            {
                _logger.LogDebug("User not found for group lookup: {Username}", username);
                return [];
            }

            // Get member of groups
            var memberOf = await _graphClient.Value.Users[user.Id].MemberOf
                .GetAsync(config =>
                {
                    config.QueryParameters.Select = ["displayName", "id"];
                }, cancellationToken);

            var groups = (memberOf?.Value ?? [])
                .OfType<Group>()
                .Select(g => g.DisplayName ?? g.Id ?? string.Empty)
                .Where(g => !string.IsNullOrEmpty(g))
                .ToList();

            RecordOperation("GetUserGroups", sw.ElapsedMilliseconds);
            return groups;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetUserGroups", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error getting groups for user: {Username}", username);
            throw new InvalidOperationException($"Failed to get user groups: {ex.Message}", ex);
        }
    }

    #region Paginated Search Methods

    /// <inheritdoc />
    public PagedResult<LdapUser> GetAllUsers(int page = 1, int pageSize = 50)
    {
        return GetAllUsersAsync(page, pageSize).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<PagedResult<LdapUser>> GetAllUsersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await FetchUsersPagedAsync(null, page, pageSize, cancellationToken);

            RecordOperation("GetAllUsers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "GetAllUsers", "user", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetAllUsers", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error getting all users");
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public PagedResult<LdapUser> SearchUsers(LdapSearchCriteria criteria, int page = 1, int pageSize = 50)
    {
        return SearchUsersAsync(criteria, page, pageSize).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<PagedResult<LdapUser>> SearchUsersAsync(LdapSearchCriteria criteria, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(criteria);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var filter = BuildODataFilter(criteria);
            var result = await FetchUsersPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("SearchUsers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "SearchUsers", "user", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchUsers", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching users with criteria");
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    private async Task<PagedResult<LdapUser>> FetchUsersPagedAsync(string? filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        // Microsoft Graph doesn't support $skip, so we need to iterate through pages
        // For efficiency, we request larger batches and skip in memory
        var skip = (page - 1) * pageSize;
        var allUsers = new List<User>();
        var totalCount = -1;

        // First request to get count
        var response = await _graphClient.Value.Users
            .GetAsync(config =>
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    config.QueryParameters.Filter = filter;
                }
                config.QueryParameters.Select = _options.SelectProperties.ToArray();
                config.QueryParameters.Top = 999; // Max allowed by Graph API
                config.QueryParameters.Count = true;
                config.Headers.Add("ConsistencyLevel", "eventual");
            }, cancellationToken);

        if (response?.Value != null)
        {
            allUsers.AddRange(response.Value);
            totalCount = (int)(response.OdataCount ?? allUsers.Count);
        }

        // If we need more pages and there's a next link, keep fetching
        while (response?.OdataNextLink != null && allUsers.Count < skip + pageSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            response = await _graphClient.Value.Users
                .WithUrl(response.OdataNextLink)
                .GetAsync(cancellationToken: cancellationToken);

            if (response?.Value != null)
            {
                allUsers.AddRange(response.Value);
            }
        }

        // Apply pagination in memory
        var pagedUsers = allUsers.Skip(skip).Take(pageSize).Select(MapToLdapUser).ToList();

        return PagedResult<LdapUser>.Create(pagedUsers, page, pageSize, totalCount);
    }

    /// <inheritdoc />
    public PagedResult<LdapUser> GetGroupMembers(string groupDnOrName, int page = 1, int pageSize = 50)
    {
        return GetGroupMembersAsync(groupDnOrName, page, pageSize).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<PagedResult<LdapUser>> GetGroupMembersAsync(string groupDnOrName, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(groupDnOrName);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // First, find the group by ID or display name
            var groupId = groupDnOrName;

            // If it doesn't look like a GUID, search by display name
            if (!Guid.TryParse(groupDnOrName, out _))
            {
                var groups = await _graphClient.Value.Groups
                    .GetAsync(config =>
                    {
                        config.QueryParameters.Filter = $"displayName eq '{EscapeODataFilter(groupDnOrName)}'";
                        config.QueryParameters.Select = ["id"];
                        config.QueryParameters.Top = 1;
                    }, cancellationToken);

                var group = groups?.Value?.FirstOrDefault();
                if (group == null)
                {
                    _logger.LogDebug("Group not found: {Group}", groupDnOrName);
                    return PagedResult<LdapUser>.Empty(page, pageSize);
                }
                groupId = group.Id!;
            }

            var skip = (page - 1) * pageSize;

            // Get group members
            var members = await _graphClient.Value.Groups[groupId].Members
                .GetAsync(config =>
                {
                    config.QueryParameters.Top = pageSize;
                    config.QueryParameters.Skip = skip;
                    config.QueryParameters.Count = true;
                    config.Headers.Add("ConsistencyLevel", "eventual");
                }, cancellationToken);

            // Filter only users and map them
            var userMembers = (members?.Value ?? [])
                .OfType<User>()
                .Select(MapToLdapUser)
                .ToList();

            var totalCount = (int)(members?.OdataCount ?? -1);

            RecordOperation("GetGroupMembers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "GetGroupMembers", "user", userMembers.Count, page, pageSize, sw.ElapsedMilliseconds);

            return PagedResult<LdapUser>.Create(userMembers, page, pageSize, totalCount);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetGroupMembers", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error getting group members: {Group}", groupDnOrName);
            throw new InvalidOperationException($"Failed to get group members: {ex.Message}", ex);
        }
    }

    private static string BuildODataFilter(LdapSearchCriteria criteria)
    {
        var filters = new List<string>();

        if (!string.IsNullOrEmpty(criteria.Username))
        {
            var escaped = EscapeODataFilter(criteria.Username);
            if (criteria.Username.Contains('*'))
            {
                var pattern = escaped.Replace("*", "");
                filters.Add($"startsWith(mailNickname, '{pattern}') or startsWith(userPrincipalName, '{pattern}')");
            }
            else
            {
                filters.Add($"(mailNickname eq '{escaped}' or userPrincipalName eq '{escaped}')");
            }
        }

        if (!string.IsNullOrEmpty(criteria.DisplayName))
        {
            var escaped = EscapeODataFilter(criteria.DisplayName);
            if (criteria.DisplayName.Contains('*'))
            {
                var pattern = escaped.Replace("*", "");
                filters.Add($"startsWith(displayName, '{pattern}')");
            }
            else
            {
                filters.Add($"displayName eq '{escaped}'");
            }
        }

        if (!string.IsNullOrEmpty(criteria.FirstName))
        {
            var escaped = EscapeODataFilter(criteria.FirstName);
            if (criteria.FirstName.Contains('*'))
            {
                var pattern = escaped.Replace("*", "");
                filters.Add($"startsWith(givenName, '{pattern}')");
            }
            else
            {
                filters.Add($"givenName eq '{escaped}'");
            }
        }

        if (!string.IsNullOrEmpty(criteria.LastName))
        {
            var escaped = EscapeODataFilter(criteria.LastName);
            if (criteria.LastName.Contains('*'))
            {
                var pattern = escaped.Replace("*", "");
                filters.Add($"startsWith(surname, '{pattern}')");
            }
            else
            {
                filters.Add($"surname eq '{escaped}'");
            }
        }

        if (!string.IsNullOrEmpty(criteria.Email))
        {
            var escaped = EscapeODataFilter(criteria.Email);
            filters.Add($"mail eq '{escaped}'");
        }

        if (!string.IsNullOrEmpty(criteria.Department))
        {
            filters.Add($"department eq '{EscapeODataFilter(criteria.Department)}'");
        }

        if (!string.IsNullOrEmpty(criteria.JobTitle))
        {
            var escaped = EscapeODataFilter(criteria.JobTitle);
            if (criteria.JobTitle.Contains('*'))
            {
                var pattern = escaped.Replace("*", "");
                filters.Add($"startsWith(jobTitle, '{pattern}')");
            }
            else
            {
                filters.Add($"jobTitle eq '{escaped}'");
            }
        }

        if (!string.IsNullOrEmpty(criteria.Company))
        {
            filters.Add($"companyName eq '{EscapeODataFilter(criteria.Company)}'");
        }

        if (!string.IsNullOrEmpty(criteria.City))
        {
            filters.Add($"city eq '{EscapeODataFilter(criteria.City)}'");
        }

        if (!string.IsNullOrEmpty(criteria.Country))
        {
            filters.Add($"country eq '{EscapeODataFilter(criteria.Country)}'");
        }

        if (criteria.IsEnabled.HasValue)
        {
            filters.Add($"accountEnabled eq {criteria.IsEnabled.Value.ToString().ToLowerInvariant()}");
        }

        if (!string.IsNullOrEmpty(criteria.CustomFilter))
        {
            filters.Add(criteria.CustomFilter);
        }

        return string.Join(" and ", filters);
    }

    #endregion

    #region Group Search Methods

    /// <inheritdoc />
    public LdapGroup? GetGroupByName(string groupName)
    {
        return GetGroupByNameAsync(groupName).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapGroup?> GetGroupByNameAsync(string groupName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(groupName);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var escapedName = EscapeODataFilter(groupName);
            var filter = $"displayName eq '{escapedName}' or mailNickname eq '{escapedName}'";

            var groups = await _graphClient.Value.Groups
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = filter;
                    config.QueryParameters.Select = GetGroupSelectProperties();
                    config.QueryParameters.Top = 1;
                }, cancellationToken);

            var graphGroup = groups?.Value?.FirstOrDefault();
            if (graphGroup == null)
            {
                _logger.LogDebug("Group not found in Azure AD: {GroupName}", groupName);
                RecordOperation("GetGroupByName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByName", "group", 0, sw.ElapsedMilliseconds, true);
                return null;
            }

            var group = MapToLdapGroup(graphGroup);
            RecordOperation("GetGroupByName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByName", "group", 1, sw.ElapsedMilliseconds, true);

            return group;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetGroupByName", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching for group: {GroupName}", groupName);
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public LdapGroup? GetGroupByDistinguishedName(string distinguishedNameOrId)
    {
        return GetGroupByDistinguishedNameAsync(distinguishedNameOrId).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapGroup?> GetGroupByDistinguishedNameAsync(string distinguishedNameOrId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(distinguishedNameOrId);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var graphGroup = await _graphClient.Value.Groups[distinguishedNameOrId]
                .GetAsync(config =>
                {
                    config.QueryParameters.Select = GetGroupSelectProperties();
                }, cancellationToken);

            if (graphGroup == null)
            {
                RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByDistinguishedName", "group", 0, sw.ElapsedMilliseconds, true);
                return null;
            }

            var group = MapToLdapGroup(graphGroup);
            RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByDistinguishedName", "group", 1, sw.ElapsedMilliseconds, true);

            return group;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByDistinguishedName", "group", 0, sw.ElapsedMilliseconds, true);
            return null;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetGroupByDistinguishedName", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching for group by ID: {Id}", distinguishedNameOrId);
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public IEnumerable<LdapGroup> SearchGroups(string searchFilter, int maxResults = 100)
    {
        return SearchGroupsAsync(searchFilter, maxResults).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LdapGroup>> SearchGroupsAsync(string searchFilter, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(searchFilter);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var groups = await _graphClient.Value.Groups
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = searchFilter;
                    config.QueryParameters.Select = GetGroupSelectProperties();
                    config.QueryParameters.Top = maxResults;
                }, cancellationToken);

            var result = (groups?.Value ?? [])
                .Select(MapToLdapGroup)
                .ToList();

            RecordOperation("SearchGroups", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "SearchGroups", "group", result.Count, sw.ElapsedMilliseconds, true);

            return result;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchGroups", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching groups with filter: {Filter}", searchFilter);
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public PagedResult<LdapGroup> GetAllGroups(int page = 1, int pageSize = 50)
    {
        return GetAllGroupsAsync(page, pageSize).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<PagedResult<LdapGroup>> GetAllGroupsAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await FetchGroupsPagedAsync(null, page, pageSize, cancellationToken);

            RecordOperation("GetAllGroups", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "GetAllGroups", "group", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetAllGroups", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error getting all groups");
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public PagedResult<LdapGroup> SearchGroups(LdapGroupSearchCriteria criteria, int page = 1, int pageSize = 50)
    {
        return SearchGroupsAsync(criteria, page, pageSize).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<PagedResult<LdapGroup>> SearchGroupsAsync(LdapGroupSearchCriteria criteria, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(criteria);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var filter = BuildGroupODataFilter(criteria);
            var result = await FetchGroupsPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("SearchGroups", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "SearchGroups", "group", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchGroups", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching groups with criteria");
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    private async Task<PagedResult<LdapGroup>> FetchGroupsPagedAsync(string? filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var skip = (page - 1) * pageSize;
        var allGroups = new List<Group>();
        var totalCount = -1;

        var response = await _graphClient.Value.Groups
            .GetAsync(config =>
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    config.QueryParameters.Filter = filter;
                }
                config.QueryParameters.Select = GetGroupSelectProperties();
                config.QueryParameters.Top = 999;
                config.QueryParameters.Count = true;
                config.Headers.Add("ConsistencyLevel", "eventual");
            }, cancellationToken);

        if (response?.Value != null)
        {
            allGroups.AddRange(response.Value);
            totalCount = (int)(response.OdataCount ?? allGroups.Count);
        }

        while (response?.OdataNextLink != null && allGroups.Count < skip + pageSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            response = await _graphClient.Value.Groups
                .WithUrl(response.OdataNextLink)
                .GetAsync(cancellationToken: cancellationToken);

            if (response?.Value != null)
            {
                allGroups.AddRange(response.Value);
            }
        }

        var pagedGroups = allGroups.Skip(skip).Take(pageSize).Select(MapToLdapGroup).ToList();

        return PagedResult<LdapGroup>.Create(pagedGroups, page, pageSize, totalCount);
    }

    private static string BuildGroupODataFilter(LdapGroupSearchCriteria criteria)
    {
        var filters = new List<string>();

        if (!string.IsNullOrEmpty(criteria.Name))
        {
            var escaped = EscapeODataFilter(criteria.Name);
            if (criteria.Name.Contains('*'))
            {
                var pattern = escaped.Replace("*", "");
                filters.Add($"startsWith(displayName, '{pattern}')");
            }
            else
            {
                filters.Add($"displayName eq '{escaped}'");
            }
        }

        if (!string.IsNullOrEmpty(criteria.DisplayName))
        {
            var escaped = EscapeODataFilter(criteria.DisplayName);
            if (criteria.DisplayName.Contains('*'))
            {
                var pattern = escaped.Replace("*", "");
                filters.Add($"startsWith(displayName, '{pattern}')");
            }
            else
            {
                filters.Add($"displayName eq '{escaped}'");
            }
        }

        if (!string.IsNullOrEmpty(criteria.Description))
        {
            var escaped = EscapeODataFilter(criteria.Description);
            filters.Add($"description eq '{escaped}'");
        }

        if (!string.IsNullOrEmpty(criteria.Email))
        {
            var escaped = EscapeODataFilter(criteria.Email);
            filters.Add($"mail eq '{escaped}'");
        }

        if (criteria.IsSecurityGroup.HasValue)
        {
            filters.Add($"securityEnabled eq {criteria.IsSecurityGroup.Value.ToString().ToLowerInvariant()}");
        }

        if (criteria.IsMailEnabled.HasValue)
        {
            filters.Add($"mailEnabled eq {criteria.IsMailEnabled.Value.ToString().ToLowerInvariant()}");
        }

        if (!string.IsNullOrEmpty(criteria.CustomFilter))
        {
            filters.Add(criteria.CustomFilter);
        }

        return string.Join(" and ", filters);
    }

    private static LdapGroup MapToLdapGroup(Group graphGroup)
    {
        var groupTypes = graphGroup.GroupTypes ?? [];
        var isUnified = groupTypes.Contains("Unified");

        string? groupType = null;
        if (isUnified) groupType = "Unified";
        else if (graphGroup.SecurityEnabled == true && graphGroup.MailEnabled != true) groupType = "Security";
        else if (graphGroup.MailEnabled == true) groupType = "MailEnabled";

        return new LdapGroup
        {
            DirectoryType = LdapDirectoryType.AzureActiveDirectory,
            Id = graphGroup.Id,
            Name = graphGroup.MailNickname ?? graphGroup.DisplayName ?? string.Empty,
            DisplayName = graphGroup.DisplayName,
            Description = graphGroup.Description,
            Email = graphGroup.Mail,
            GroupType = groupType,
            IsSecurityGroup = graphGroup.SecurityEnabled,
            IsMailEnabled = graphGroup.MailEnabled,
            CreatedAt = graphGroup.CreatedDateTime
        };
    }

    private static string[] GetGroupSelectProperties() =>
    [
        "id",
        "displayName",
        "mailNickname",
        "description",
        "mail",
        "securityEnabled",
        "mailEnabled",
        "groupTypes",
        "createdDateTime"
    ];

    #endregion

    #region Computer Search Methods

    /// <inheritdoc />
    public LdapComputer? GetComputerByName(string computerName)
    {
        return GetComputerByNameAsync(computerName).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapComputer?> GetComputerByNameAsync(string computerName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(computerName);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var escapedName = EscapeODataFilter(computerName);
            var filter = $"displayName eq '{escapedName}'";

            var devices = await _graphClient.Value.Devices
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = filter;
                    config.QueryParameters.Select = GetDeviceSelectProperties();
                    config.QueryParameters.Top = 1;
                }, cancellationToken);

            var graphDevice = devices?.Value?.FirstOrDefault();
            if (graphDevice == null)
            {
                _logger.LogDebug("Device not found in Azure AD: {ComputerName}", computerName);
                RecordOperation("GetComputerByName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByName", "computer", 0, sw.ElapsedMilliseconds, true);
                return null;
            }

            var computer = MapToLdapComputer(graphDevice);
            RecordOperation("GetComputerByName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByName", "computer", 1, sw.ElapsedMilliseconds, true);

            return computer;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetComputerByName", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching for device: {ComputerName}", computerName);
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public LdapComputer? GetComputerByDistinguishedName(string distinguishedNameOrId)
    {
        return GetComputerByDistinguishedNameAsync(distinguishedNameOrId).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapComputer?> GetComputerByDistinguishedNameAsync(string distinguishedNameOrId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(distinguishedNameOrId);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var graphDevice = await _graphClient.Value.Devices[distinguishedNameOrId]
                .GetAsync(config =>
                {
                    config.QueryParameters.Select = GetDeviceSelectProperties();
                }, cancellationToken);

            if (graphDevice == null)
            {
                RecordOperation("GetComputerByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByDistinguishedName", "computer", 0, sw.ElapsedMilliseconds, true);
                return null;
            }

            var computer = MapToLdapComputer(graphDevice);
            RecordOperation("GetComputerByDistinguishedName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByDistinguishedName", "computer", 1, sw.ElapsedMilliseconds, true);

            return computer;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            RecordOperation("GetComputerByDistinguishedName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByDistinguishedName", "computer", 0, sw.ElapsedMilliseconds, true);
            return null;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetComputerByDistinguishedName", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching for device by ID: {Id}", distinguishedNameOrId);
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public IEnumerable<LdapComputer> SearchComputers(string searchFilter, int maxResults = 100)
    {
        return SearchComputersAsync(searchFilter, maxResults).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LdapComputer>> SearchComputersAsync(string searchFilter, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(searchFilter);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var devices = await _graphClient.Value.Devices
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = searchFilter;
                    config.QueryParameters.Select = GetDeviceSelectProperties();
                    config.QueryParameters.Top = maxResults;
                }, cancellationToken);

            var result = (devices?.Value ?? [])
                .Select(MapToLdapComputer)
                .ToList();

            RecordOperation("SearchComputers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "SearchComputers", "computer", result.Count, sw.ElapsedMilliseconds, true);

            return result;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchComputers", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching devices with filter: {Filter}", searchFilter);
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public PagedResult<LdapComputer> GetAllComputers(int page = 1, int pageSize = 50)
    {
        return GetAllComputersAsync(page, pageSize).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<PagedResult<LdapComputer>> GetAllComputersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await FetchDevicesPagedAsync(null, page, pageSize, cancellationToken);

            RecordOperation("GetAllComputers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "GetAllComputers", "computer", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetAllComputers", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error getting all devices");
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public PagedResult<LdapComputer> SearchComputers(LdapComputerSearchCriteria criteria, int page = 1, int pageSize = 50)
    {
        return SearchComputersAsync(criteria, page, pageSize).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<PagedResult<LdapComputer>> SearchComputersAsync(LdapComputerSearchCriteria criteria, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(criteria);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var filter = BuildDeviceODataFilter(criteria);
            var result = await FetchDevicesPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("SearchComputers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "SearchComputers", "computer", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchComputers", ex.GetType().Name);
            _logger.LogError(ex, "Graph API error searching devices with criteria");
            throw new InvalidOperationException($"Azure AD query failed: {ex.Message}", ex);
        }
    }

    private async Task<PagedResult<LdapComputer>> FetchDevicesPagedAsync(string? filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var skip = (page - 1) * pageSize;
        var allDevices = new List<Device>();
        var totalCount = -1;

        var response = await _graphClient.Value.Devices
            .GetAsync(config =>
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    config.QueryParameters.Filter = filter;
                }
                config.QueryParameters.Select = GetDeviceSelectProperties();
                config.QueryParameters.Top = 999;
                config.QueryParameters.Count = true;
                config.Headers.Add("ConsistencyLevel", "eventual");
            }, cancellationToken);

        if (response?.Value != null)
        {
            allDevices.AddRange(response.Value);
            totalCount = (int)(response.OdataCount ?? allDevices.Count);
        }

        while (response?.OdataNextLink != null && allDevices.Count < skip + pageSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            response = await _graphClient.Value.Devices
                .WithUrl(response.OdataNextLink)
                .GetAsync(cancellationToken: cancellationToken);

            if (response?.Value != null)
            {
                allDevices.AddRange(response.Value);
            }
        }

        var pagedDevices = allDevices.Skip(skip).Take(pageSize).Select(MapToLdapComputer).ToList();

        return PagedResult<LdapComputer>.Create(pagedDevices, page, pageSize, totalCount);
    }

    private static string BuildDeviceODataFilter(LdapComputerSearchCriteria criteria)
    {
        var filters = new List<string>();

        if (!string.IsNullOrEmpty(criteria.Name))
        {
            var escaped = EscapeODataFilter(criteria.Name);
            if (criteria.Name.Contains('*'))
            {
                var pattern = escaped.Replace("*", "");
                filters.Add($"startsWith(displayName, '{pattern}')");
            }
            else
            {
                filters.Add($"displayName eq '{escaped}'");
            }
        }

        if (!string.IsNullOrEmpty(criteria.DisplayName))
        {
            var escaped = EscapeODataFilter(criteria.DisplayName);
            if (criteria.DisplayName.Contains('*'))
            {
                var pattern = escaped.Replace("*", "");
                filters.Add($"startsWith(displayName, '{pattern}')");
            }
            else
            {
                filters.Add($"displayName eq '{escaped}'");
            }
        }

        if (!string.IsNullOrEmpty(criteria.OperatingSystem))
        {
            var escaped = EscapeODataFilter(criteria.OperatingSystem);
            if (criteria.OperatingSystem.Contains('*'))
            {
                var pattern = escaped.Replace("*", "");
                filters.Add($"startsWith(operatingSystem, '{pattern}')");
            }
            else
            {
                filters.Add($"operatingSystem eq '{escaped}'");
            }
        }

        if (!string.IsNullOrEmpty(criteria.OperatingSystemVersion))
        {
            var escaped = EscapeODataFilter(criteria.OperatingSystemVersion);
            filters.Add($"operatingSystemVersion eq '{escaped}'");
        }

        if (criteria.IsEnabled.HasValue)
        {
            filters.Add($"accountEnabled eq {criteria.IsEnabled.Value.ToString().ToLowerInvariant()}");
        }

        if (criteria.IsManaged.HasValue)
        {
            filters.Add($"isManaged eq {criteria.IsManaged.Value.ToString().ToLowerInvariant()}");
        }

        if (criteria.IsCompliant.HasValue)
        {
            filters.Add($"isCompliant eq {criteria.IsCompliant.Value.ToString().ToLowerInvariant()}");
        }

        if (!string.IsNullOrEmpty(criteria.TrustType))
        {
            filters.Add($"trustType eq '{EscapeODataFilter(criteria.TrustType)}'");
        }

        if (!string.IsNullOrEmpty(criteria.CustomFilter))
        {
            filters.Add(criteria.CustomFilter);
        }

        return string.Join(" and ", filters);
    }

    private static LdapComputer MapToLdapComputer(Device graphDevice)
    {
        return new LdapComputer
        {
            DirectoryType = LdapDirectoryType.AzureActiveDirectory,
            Id = graphDevice.DeviceId,
            Name = graphDevice.DisplayName ?? string.Empty,
            DisplayName = graphDevice.DisplayName,
            OperatingSystem = graphDevice.OperatingSystem,
            OperatingSystemVersion = graphDevice.OperatingSystemVersion,
            IsEnabled = graphDevice.AccountEnabled,
            IsManaged = graphDevice.IsManaged,
            IsCompliant = graphDevice.IsCompliant,
            TrustType = graphDevice.TrustType,
            LastLogon = graphDevice.ApproximateLastSignInDateTime
        };
    }

    private static string[] GetDeviceSelectProperties() =>
    [
        "id",
        "deviceId",
        "displayName",
        "operatingSystem",
        "operatingSystemVersion",
        "accountEnabled",
        "isManaged",
        "isCompliant",
        "trustType",
        "approximateLastSignInDateTime"
    ];

    #endregion

    #region Advanced Authentication Methods

    /// <inheritdoc />
    public LdapAuthenticationResult Authenticate(LdapAuthenticationOptions options)
    {
        return AuthenticateAsync(options).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Azure AD does not support traditional LDAP authentication modes.
    /// Only <see cref="LdapAuthenticationMode.Simple"/> is supported, which maps to
    /// Resource Owner Password Credentials (ROPC) flow (deprecated and not recommended).
    /// </para>
    /// <para>
    /// For production scenarios, use:
    /// <list type="bullet">
    ///   <item><see cref="AuthenticateWithDeviceCodeAsync"/> for CLI/IoT scenarios</item>
    ///   <item><see cref="AuthenticateWithInteractiveBrowserAsync"/> for desktop apps</item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task<LdapAuthenticationResult> AuthenticateAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            options.Validate();

            var result = options.Mode switch
            {
                LdapAuthenticationMode.Simple => await AuthenticateWithRopcAsync(options, cancellationToken),
                LdapAuthenticationMode.Certificate => await AuthenticateWithCertificateInternalAsync(options, cancellationToken),
                _ => throw new NotSupportedException(
                    $"Authentication mode {options.Mode} is not supported by Azure AD. " +
                    "Use AuthenticateWithDeviceCodeAsync or AuthenticateWithInteractiveBrowserAsync instead.")
            };

            RecordOperation("Authenticate", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, options.Mode.ToString(), result.IsAuthenticated);

            return result;
        }
        catch (Exception ex) when (ex is not NotSupportedException && ex is not InvalidOperationException && ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "Authenticate", ex.GetType().Name);
            _logger.LogError(ex, "Azure AD authentication failed with mode {Mode}", options.Mode);
            throw new InvalidOperationException($"Authentication failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Kerberos is not supported by Azure AD.</exception>
    public Task<LdapAuthenticationResult> AuthenticateWithKerberosAsync(
        string? username = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Kerberos authentication is not supported by Azure AD. " +
            "Use AuthenticateWithDeviceCodeAsync or AuthenticateWithInteractiveBrowserAsync instead.");
    }

    /// <inheritdoc />
    public async Task<LdapAuthenticationResult> AuthenticateWithCertificateAsync(
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(certificate);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var options = new LdapAuthenticationOptions
            {
                Mode = LdapAuthenticationMode.Certificate,
                Certificate = certificate
            };

            var result = await AuthenticateWithCertificateInternalAsync(options, cancellationToken);

            RecordOperation("AuthenticateWithCertificate", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "Certificate", result.IsAuthenticated);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "AuthenticateWithCertificate", ex.GetType().Name);
            _logger.LogError(ex, "Certificate authentication failed");
            throw new InvalidOperationException($"Certificate authentication failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LdapAuthenticationMode> GetSupportedAuthenticationModes()
    {
        return
        [
            LdapAuthenticationMode.Simple, // Maps to ROPC
            LdapAuthenticationMode.Certificate
        ];
    }

    /// <summary>
    /// Authenticates using the OAuth2 Device Code flow.
    /// </summary>
    /// <param name="deviceCodeCallback">
    /// A callback function that receives the device code information.
    /// The callback should display the user code and verification URL to the user.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the authentication result with access token.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceCodeCallback"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when device code authentication fails.</exception>
    /// <example>
    /// <code>
    /// var result = await azureAdService.AuthenticateWithDeviceCodeAsync(async info =>
    /// {
    ///     Console.WriteLine(info.Message);
    ///     Console.WriteLine($"Go to {info.VerificationUri} and enter code: {info.UserCode}");
    ///     await Task.CompletedTask;
    /// });
    ///
    /// if (result.IsAuthenticated)
    /// {
    ///     Console.WriteLine($"Authenticated as {result.Username}");
    ///     // Use result.Token for API calls
    /// }
    /// </code>
    /// </example>
    public async Task<LdapAuthenticationResult> AuthenticateWithDeviceCodeAsync(
        Func<Options.DeviceCodeInfo, Task> deviceCodeCallback,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(deviceCodeCallback);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var scopes = _options.Scopes?.ToArray() ?? ["https://graph.microsoft.com/.default"];

            var credential = new DeviceCodeCredential(
                async (codeInfo, ct) =>
                {
                    var info = new Options.DeviceCodeInfo
                    {
                        UserCode = codeInfo.UserCode,
                        VerificationUri = codeInfo.VerificationUri.ToString(),
                        VerificationUriComplete = codeInfo.VerificationUri.ToString() + "?otc=" + codeInfo.UserCode,
                        Message = codeInfo.Message,
                        ExpiresOn = codeInfo.ExpiresOn,
                        ClientId = codeInfo.ClientId
                    };
                    await deviceCodeCallback(info);
                },
                _options.TenantId,
                _options.ClientId);

            var tokenRequest = new Azure.Core.TokenRequestContext(scopes);
            var token = await credential.GetTokenAsync(tokenRequest, cancellationToken);

            // Get user info using the token
            var graphClient = new GraphServiceClient(credential, scopes);
            var me = await graphClient.Me.GetAsync(cancellationToken: cancellationToken);

            var result = new LdapAuthenticationResult
            {
                IsAuthenticated = true,
                Username = me?.UserPrincipalName ?? me?.Mail ?? "unknown",
                UserId = me?.Id,
                Email = me?.Mail,
                DisplayName = me?.DisplayName,
                AuthenticationMode = LdapAuthenticationMode.Simple, // Mapped
                DirectoryType = LdapDirectoryType.AzureActiveDirectory,
                Token = token.Token,
                ExpiresAt = token.ExpiresOn,
                AuthenticatedAt = DateTimeOffset.UtcNow
            };

            RecordOperation("AuthenticateWithDeviceCode", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "DeviceCode", true);

            _logger.LogDebug("Device code authentication succeeded for user: {Username}", result.Username);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "AuthenticateWithDeviceCode", ex.GetType().Name);
            _logger.LogError(ex, "Device code authentication failed");
            return LdapAuthenticationResult.Failure(
                $"Device code authentication failed: {ex.Message}",
                null,
                LdapAuthenticationMode.Simple,
                LdapDirectoryType.AzureActiveDirectory);
        }
    }

    /// <summary>
    /// Authenticates using the OAuth2 Interactive Browser flow.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the authentication result with access token.</returns>
    /// <remarks>
    /// <para>
    /// This method opens a browser window for the user to authenticate.
    /// Best suited for desktop applications.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var result = await azureAdService.AuthenticateWithInteractiveBrowserAsync();
    /// if (result.IsAuthenticated)
    /// {
    ///     Console.WriteLine($"Welcome, {result.DisplayName}!");
    /// }
    /// </code>
    /// </example>
    public async Task<LdapAuthenticationResult> AuthenticateWithInteractiveBrowserAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var scopes = _options.Scopes?.ToArray() ?? ["https://graph.microsoft.com/.default"];

            var credentialOptions = new InteractiveBrowserCredentialOptions
            {
                TenantId = _options.TenantId,
                ClientId = _options.ClientId,
                RedirectUri = _options.RedirectUri ?? new Uri("http://localhost")
            };

            var credential = new InteractiveBrowserCredential(credentialOptions);

            var tokenRequest = new Azure.Core.TokenRequestContext(scopes);
            var token = await credential.GetTokenAsync(tokenRequest, cancellationToken);

            // Get user info using the token
            var graphClient = new GraphServiceClient(credential, scopes);
            var me = await graphClient.Me.GetAsync(cancellationToken: cancellationToken);

            var result = new LdapAuthenticationResult
            {
                IsAuthenticated = true,
                Username = me?.UserPrincipalName ?? me?.Mail ?? "unknown",
                UserId = me?.Id,
                Email = me?.Mail,
                DisplayName = me?.DisplayName,
                AuthenticationMode = LdapAuthenticationMode.Simple,
                DirectoryType = LdapDirectoryType.AzureActiveDirectory,
                Token = token.Token,
                ExpiresAt = token.ExpiresOn,
                AuthenticatedAt = DateTimeOffset.UtcNow
            };

            RecordOperation("AuthenticateWithInteractiveBrowser", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "InteractiveBrowser", true);

            _logger.LogDebug("Interactive browser authentication succeeded for user: {Username}", result.Username);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "AuthenticateWithInteractiveBrowser", ex.GetType().Name);
            _logger.LogError(ex, "Interactive browser authentication failed");
            return LdapAuthenticationResult.Failure(
                $"Interactive browser authentication failed: {ex.Message}",
                null,
                LdapAuthenticationMode.Simple,
                LdapDirectoryType.AzureActiveDirectory);
        }
    }

    /// <summary>
    /// Authenticates using the Resource Owner Password Credentials (ROPC) flow.
    /// </summary>
    /// <param name="username">The user's username or email.</param>
    /// <param name="password">The user's password.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the authentication result.</returns>
    /// <remarks>
    /// <para>
    /// ROPC is a legacy flow and is NOT recommended for production use.
    /// It does not support MFA and has security limitations.
    /// Use <see cref="AuthenticateWithDeviceCodeAsync"/> or
    /// <see cref="AuthenticateWithInteractiveBrowserAsync"/> instead.
    /// </para>
    /// </remarks>
    public async Task<LdapAuthenticationResult> AuthenticateWithUsernamePasswordAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogWarning(
            "Using ROPC flow for authentication. This is deprecated and not recommended. " +
            "Consider using AuthenticateWithDeviceCodeAsync or AuthenticateWithInteractiveBrowserAsync.");

        try
        {
            var scopes = _options.Scopes?.ToArray() ?? ["https://graph.microsoft.com/.default"];

            var credential = new UsernamePasswordCredential(
                username,
                password,
                _options.TenantId,
                _options.ClientId);

            var tokenRequest = new Azure.Core.TokenRequestContext(scopes);
            var token = await credential.GetTokenAsync(tokenRequest, cancellationToken);

            // Get user info
            var graphClient = new GraphServiceClient(credential, scopes);
            var me = await graphClient.Me.GetAsync(cancellationToken: cancellationToken);

            var result = new LdapAuthenticationResult
            {
                IsAuthenticated = true,
                Username = me?.UserPrincipalName ?? username,
                UserId = me?.Id,
                Email = me?.Mail,
                DisplayName = me?.DisplayName,
                AuthenticationMode = LdapAuthenticationMode.Simple,
                DirectoryType = LdapDirectoryType.AzureActiveDirectory,
                Token = token.Token,
                ExpiresAt = token.ExpiresOn,
                AuthenticatedAt = DateTimeOffset.UtcNow
            };

            RecordOperation("AuthenticateWithUsernamePassword", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "UsernamePassword", true);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "AuthenticateWithUsernamePassword", ex.GetType().Name);
            _logger.LogError(ex, "Username/password authentication failed for user: {Username}", username);
            return LdapAuthenticationResult.Failure(
                $"Authentication failed: {ex.Message}",
                null,
                LdapAuthenticationMode.Simple,
                LdapDirectoryType.AzureActiveDirectory);
        }
    }

    private async Task<LdapAuthenticationResult> AuthenticateWithRopcAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        return await AuthenticateWithUsernamePasswordAsync(
            options.Username!,
            options.Password!,
            cancellationToken);
    }

    private async Task<LdapAuthenticationResult> AuthenticateWithCertificateInternalAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        var certificate = options.GetCertificate();
        if (certificate == null)
        {
            throw new InvalidOperationException("Certificate is required for certificate authentication.");
        }

        try
        {
            var scopes = _options.Scopes?.ToArray() ?? ["https://graph.microsoft.com/.default"];

            var credential = new ClientCertificateCredential(
                _options.TenantId,
                _options.ClientId,
                certificate);

            var tokenRequest = new Azure.Core.TokenRequestContext(scopes);
            var token = await credential.GetTokenAsync(tokenRequest, cancellationToken);

            // For service principal auth, we can't get /me
            var result = new LdapAuthenticationResult
            {
                IsAuthenticated = true,
                Username = _options.ClientId,
                AuthenticationMode = LdapAuthenticationMode.Certificate,
                DirectoryType = LdapDirectoryType.AzureActiveDirectory,
                Token = token.Token,
                ExpiresAt = token.ExpiresOn,
                AuthenticatedAt = DateTimeOffset.UtcNow
            };

            _logger.LogDebug("Certificate authentication succeeded for client: {ClientId}", _options.ClientId);
            return result;
        }
        catch (Exception ex)
        {
            return LdapAuthenticationResult.Failure(
                $"Certificate authentication failed: {ex.Message}",
                null,
                LdapAuthenticationMode.Certificate,
                LdapDirectoryType.AzureActiveDirectory);
        }
    }

    #endregion

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        // GraphServiceClient doesn't need explicit disposal
        return ValueTask.CompletedTask;
    }

    private GraphServiceClient CreateGraphClient()
    {
        try
        {
            var credential = _options.AuthenticationMode switch
            {
                AzureAdAuthMode.ClientSecret when !string.IsNullOrEmpty(_options.ClientSecret) =>
                    new ClientSecretCredential(_options.TenantId, _options.ClientId, _options.ClientSecret),

                AzureAdAuthMode.Certificate => CreateCertificateCredential(),

                AzureAdAuthMode.ManagedIdentity or _ when _options.UseManagedIdentity =>
                    (Azure.Core.TokenCredential)new ManagedIdentityCredential(),

                _ => throw new InvalidOperationException(
                    "No valid authentication method configured. " +
                    "Provide ClientSecret, Certificate, or enable ManagedIdentity.")
            };

            var client = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
            ToolboxMeter.RecordLdapConnection(ServiceName, "AzureActiveDirectory", "graph.microsoft.com", true);
            return client;
        }
        catch (Exception ex)
        {
            ToolboxMeter.RecordLdapConnection(ServiceName, "AzureActiveDirectory", "graph.microsoft.com", false);
            ToolboxMeter.RecordLdapError(ServiceName, "CreateGraphClient", ex.GetType().Name);
            throw;
        }
    }

    private ClientCertificateCredential CreateCertificateCredential()
    {
        X509Certificate2? certificate = null;

        if (!string.IsNullOrEmpty(_options.CertificateThumbprint))
        {
            // Load from certificate store
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            var certs = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                _options.CertificateThumbprint,
                validOnly: false);

            certificate = certs.Count > 0 ? certs[0] : null;
        }
        else if (!string.IsNullOrEmpty(_options.CertificatePath))
        {
            // Load from file using X509CertificateLoader (non-obsolete API)
            certificate = string.IsNullOrEmpty(_options.CertificatePassword)
                ? X509CertificateLoader.LoadPkcs12FromFile(_options.CertificatePath, null)
                : X509CertificateLoader.LoadPkcs12FromFile(_options.CertificatePath, _options.CertificatePassword);
        }

        if (certificate == null)
        {
            throw new InvalidOperationException(
                "Certificate not found. Provide CertificateThumbprint or CertificatePath.");
        }

        return new ClientCertificateCredential(_options.TenantId, _options.ClientId, certificate);
    }

    private static LdapUser MapToLdapUser(User graphUser)
    {
        return new LdapUser
        {
            DirectoryType = LdapDirectoryType.AzureActiveDirectory,
            Id = graphUser.Id,
            Username = graphUser.MailNickname ?? graphUser.UserPrincipalName ?? string.Empty,
            UserPrincipalName = graphUser.UserPrincipalName,
            DisplayName = graphUser.DisplayName,
            FirstName = graphUser.GivenName,
            LastName = graphUser.Surname,
            Email = graphUser.Mail,
            MobilePhone = graphUser.MobilePhone,
            PhoneNumber = graphUser.BusinessPhones?.FirstOrDefault(),
            JobTitle = graphUser.JobTitle,
            Department = graphUser.Department,
            Office = graphUser.OfficeLocation,
            StreetAddress = graphUser.StreetAddress,
            City = graphUser.City,
            State = graphUser.State,
            PostalCode = graphUser.PostalCode,
            Country = graphUser.Country,
            IsEnabled = graphUser.AccountEnabled,
            CreatedAt = graphUser.CreatedDateTime
        };
    }

    private static string EscapeODataFilter(string value)
    {
        // Escape single quotes by doubling them
        return value.Replace("'", "''");
    }
}
