// @file ActiveDirectoryService.cs
// @brief Active Directory service implementation
// @details Implements ILdapService for Windows Active Directory
// @note Uses System.DirectoryServices.Protocols for cross-platform support

using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Ldap;

/// <summary>
/// LDAP service implementation for Windows Active Directory.
/// </summary>
/// <remarks>
/// <para>
/// This service provides access to Windows Active Directory using
/// the LDAP protocol via System.DirectoryServices.Protocols.
/// </para>
/// <para>
/// Features:
/// </para>
/// <list type="bullet">
///   <item><description>User lookup by username (sAMAccountName) or email</description></item>
///   <item><description>Custom LDAP filter search</description></item>
///   <item><description>Credential validation</description></item>
///   <item><description>Group membership retrieval</description></item>
///   <item><description>SSL/TLS support</description></item>
/// </list>
/// </remarks>
/// <seealso cref="ILdapService"/>
public sealed class ActiveDirectoryService : BaseAsyncDisposableService, ILdapService
{
    private readonly ActiveDirectoryOptions _options;
    private readonly ILogger<ActiveDirectoryService> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private LdapConnection? _connection;
    private bool _isConnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveDirectoryService"/> class.
    /// </summary>
    /// <param name="options">The Active Directory options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when domain is empty.</exception>
    public ActiveDirectoryService(
        IOptions<ActiveDirectoryOptions> options,
        ILogger<ActiveDirectoryService> logger)
        : base("ActiveDirectoryService", logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Domain))
        {
            throw new ArgumentException("Domain cannot be empty.", nameof(options));
        }

        _logger.LogDebug(
            "ActiveDirectoryService initialized for domain {Domain}",
            _options.Domain);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveDirectoryService"/> class.
    /// </summary>
    /// <param name="options">The Active Directory options.</param>
    /// <param name="logger">The logger instance.</param>
    public ActiveDirectoryService(
        ActiveDirectoryOptions options,
        ILogger<ActiveDirectoryService> logger)
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
            await EnsureConnectedAsync(cancellationToken);

            var filter = string.Format(_options.UserSearchFilter, EscapeLdapFilter(username));
            var user = await SearchSingleUserAsync(filter, cancellationToken);

            RecordOperation("GetUserByUsername", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetUserByUsername", "user", user != null ? 1 : 0, sw.ElapsedMilliseconds, true);

            return user;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetUserByUsername", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching for user: {Username}", username);
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
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
            await EnsureConnectedAsync(cancellationToken);

            var filter = string.Format(_options.EmailSearchFilter, EscapeLdapFilter(email));
            var user = await SearchSingleUserAsync(filter, cancellationToken);

            RecordOperation("GetUserByEmail", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetUserByEmail", "user", user != null ? 1 : 0, sw.ElapsedMilliseconds, true);

            return user;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetUserByEmail", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching for user by email: {Email}", email);
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
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
            await EnsureConnectedAsync(cancellationToken);

            var searchRequest = new SearchRequest(
                GetBaseDn(),
                searchFilter,
                SearchScope.Subtree,
                GetUserAttributes());

            searchRequest.SizeLimit = maxResults;

            var response = await Task.Run(
                () => (SearchResponse)_connection!.SendRequest(searchRequest),
                cancellationToken);

            var users = response.Entries
                .Cast<SearchResultEntry>()
                .Select(MapToLdapUser)
                .ToList();

            RecordOperation("SearchUsers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "SearchUsers", "user", users.Count, sw.ElapsedMilliseconds, true);

            return users;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchUsers", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching users with filter: {Filter}", searchFilter);
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public bool ValidateCredentials(string username, string password)
    {
        return ValidateCredentialsAsync(username, password).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var server = _options.Server ?? _options.Domain;
            var identifier = new LdapDirectoryIdentifier(server, _options.Port);

            using var connection = new LdapConnection(identifier)
            {
                Timeout = _options.ConnectionTimeout
            };

            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.SecureSocketLayer = _options.UseSsl;

            if (!_options.ValidateCertificate)
            {
                connection.SessionOptions.VerifyServerCertificate = (_, _) => true;
            }

            var credential = new NetworkCredential(username, password, _options.Domain);

            await Task.Run(() => connection.Bind(credential), cancellationToken);

            RecordOperation("ValidateCredentials", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "ActiveDirectory", true);

            _logger.LogDebug("Credentials validated for user: {Username}", username);
            return true;
        }
        catch (LdapException ex) when (ex.ErrorCode == 49) // Invalid credentials
        {
            _logger.LogDebug("Invalid credentials for user: {Username}", username);
            RecordOperation("ValidateCredentials", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "ActiveDirectory", false);
            return false;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "ValidateCredentials", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error validating credentials for user: {Username}", username);
            throw new InvalidOperationException($"Credential validation failed: {ex.Message}", ex);
        }
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
            var user = await GetUserByUsernameAsync(username, cancellationToken);

            if (user == null)
            {
                _logger.LogDebug("User not found for group lookup: {Username}", username);
                return [];
            }

            RecordOperation("GetUserGroups", sw.ElapsedMilliseconds);
            return user.Groups;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error getting groups for user: {Username}", username);
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
            await EnsureConnectedAsync(cancellationToken);

            // Filter for all user objects
            const string filter = "(&(objectClass=user)(objectCategory=person))";
            var result = await SearchUsersPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("GetAllUsers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "GetAllUsers", "user", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetAllUsers", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error getting all users");
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
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
            await EnsureConnectedAsync(cancellationToken);

            var filter = BuildLdapFilter(criteria);
            var result = await SearchUsersPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("SearchUsers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "SearchUsers", "user", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchUsers", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching users with criteria");
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
        }
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
            await EnsureConnectedAsync(cancellationToken);

            // Build filter for group membership
            var escapedGroup = EscapeLdapFilter(groupDnOrName);
            var filter = $"(&(objectClass=user)(objectCategory=person)(memberOf={escapedGroup}))";

            var result = await SearchUsersPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("GetGroupMembers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "GetGroupMembers", "user", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetGroupMembers", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error getting group members: {Group}", groupDnOrName);
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
        }
    }

    private async Task<PagedResult<LdapUser>> SearchUsersPagedAsync(string filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        // Calculate skip for pagination
        var skip = (page - 1) * pageSize;

        // First, get total count (optional - can be expensive for large directories)
        var countRequest = new SearchRequest(
            GetBaseDn(),
            filter,
            SearchScope.Subtree,
            "objectGUID"); // Only request one attribute for count

        var countResponse = await Task.Run(
            () => (SearchResponse)_connection!.SendRequest(countRequest),
            cancellationToken);

        var totalCount = countResponse.Entries.Count;

        // Now get the actual page of results
        var searchRequest = new SearchRequest(
            GetBaseDn(),
            filter,
            SearchScope.Subtree,
            GetUserAttributes());

        // Note: LDAP doesn't have native pagination like SQL
        // We retrieve all matching entries and paginate in memory
        // For very large directories, consider using VLV or paged results control
        searchRequest.SizeLimit = skip + pageSize + 1; // Get one extra to check if there are more

        var response = await Task.Run(
            () => (SearchResponse)_connection!.SendRequest(searchRequest),
            cancellationToken);

        var allEntries = response.Entries.Cast<SearchResultEntry>().ToList();
        var pagedEntries = allEntries.Skip(skip).Take(pageSize).ToList();

        var users = pagedEntries.Select(MapToLdapUser).ToList();

        return PagedResult<LdapUser>.Create(users, page, pageSize, totalCount);
    }

    private static string BuildLdapFilter(LdapSearchCriteria criteria)
    {
        var filters = new List<string>
        {
            "(objectClass=user)",
            "(objectCategory=person)"
        };

        if (!string.IsNullOrEmpty(criteria.Username))
            filters.Add($"(sAMAccountName={EscapeLdapFilterWithWildcard(criteria.Username)})");

        if (!string.IsNullOrEmpty(criteria.DisplayName))
            filters.Add($"(displayName={EscapeLdapFilterWithWildcard(criteria.DisplayName)})");

        if (!string.IsNullOrEmpty(criteria.FirstName))
            filters.Add($"(givenName={EscapeLdapFilterWithWildcard(criteria.FirstName)})");

        if (!string.IsNullOrEmpty(criteria.LastName))
            filters.Add($"(sn={EscapeLdapFilterWithWildcard(criteria.LastName)})");

        if (!string.IsNullOrEmpty(criteria.Email))
            filters.Add($"(mail={EscapeLdapFilterWithWildcard(criteria.Email)})");

        if (!string.IsNullOrEmpty(criteria.Department))
            filters.Add($"(department={EscapeLdapFilter(criteria.Department)})");

        if (!string.IsNullOrEmpty(criteria.JobTitle))
            filters.Add($"(title={EscapeLdapFilterWithWildcard(criteria.JobTitle)})");

        if (!string.IsNullOrEmpty(criteria.Company))
            filters.Add($"(company={EscapeLdapFilter(criteria.Company)})");

        if (!string.IsNullOrEmpty(criteria.Office))
            filters.Add($"(physicalDeliveryOfficeName={EscapeLdapFilter(criteria.Office)})");

        if (!string.IsNullOrEmpty(criteria.City))
            filters.Add($"(l={EscapeLdapFilter(criteria.City)})");

        if (!string.IsNullOrEmpty(criteria.Country))
            filters.Add($"(co={EscapeLdapFilter(criteria.Country)})");

        if (!string.IsNullOrEmpty(criteria.MemberOfGroup))
            filters.Add($"(memberOf={EscapeLdapFilter(criteria.MemberOfGroup)})");

        if (criteria.MemberOfAnyGroup?.Count > 0)
        {
            var groupFilters = criteria.MemberOfAnyGroup
                .Select(g => $"(memberOf={EscapeLdapFilter(g)})")
                .ToList();
            filters.Add($"(|{string.Join("", groupFilters)})");
        }

        if (criteria.IsEnabled.HasValue)
        {
            // userAccountControl: 0x2 = ACCOUNTDISABLE
            filters.Add(criteria.IsEnabled.Value
                ? "(!(userAccountControl:1.2.840.113556.1.4.803:=2))"
                : "(userAccountControl:1.2.840.113556.1.4.803:=2)");
        }

        if (criteria.CustomAttributes != null)
        {
            foreach (var (attr, value) in criteria.CustomAttributes)
            {
                filters.Add($"({EscapeLdapFilter(attr)}={EscapeLdapFilterWithWildcard(value)})");
            }
        }

        if (!string.IsNullOrEmpty(criteria.CustomFilter))
        {
            filters.Add(criteria.CustomFilter);
        }

        return $"(&{string.Join("", filters)})";
    }

    private static string EscapeLdapFilterWithWildcard(string value)
    {
        // Preserve * as wildcard, escape everything else
        return value
            .Replace("\\", "\\5c")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
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
            await EnsureConnectedAsync(cancellationToken);

            var escapedName = EscapeLdapFilter(groupName);
            var filter = $"(&(objectClass=group)(|(cn={escapedName})(sAMAccountName={escapedName})(displayName={escapedName})))";
            var group = await SearchSingleGroupAsync(filter, cancellationToken);

            RecordOperation("GetGroupByName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByName", "group", group != null ? 1 : 0, sw.ElapsedMilliseconds, true);

            return group;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetGroupByName", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching for group: {GroupName}", groupName);
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
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
            await EnsureConnectedAsync(cancellationToken);

            var searchRequest = new SearchRequest(
                distinguishedNameOrId,
                "(objectClass=group)",
                SearchScope.Base,
                GetGroupAttributes());

            searchRequest.SizeLimit = 1;

            var response = await Task.Run(
                () => (SearchResponse)_connection!.SendRequest(searchRequest),
                cancellationToken);

            if (response.Entries.Count == 0)
            {
                RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByDistinguishedName", "group", 0, sw.ElapsedMilliseconds, true);
                return null;
            }

            var group = MapToLdapGroup((SearchResultEntry)response.Entries[0]!);

            RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByDistinguishedName", "group", 1, sw.ElapsedMilliseconds, true);

            return group;
        }
        catch (LdapException ex) when (ex.ErrorCode == 32) // No such object
        {
            RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByDistinguishedName", "group", 0, sw.ElapsedMilliseconds, true);
            return null;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetGroupByDistinguishedName", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching for group by DN: {DN}", distinguishedNameOrId);
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
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
            await EnsureConnectedAsync(cancellationToken);

            var searchRequest = new SearchRequest(
                GetBaseDn(),
                searchFilter,
                SearchScope.Subtree,
                GetGroupAttributes());

            searchRequest.SizeLimit = maxResults;

            var response = await Task.Run(
                () => (SearchResponse)_connection!.SendRequest(searchRequest),
                cancellationToken);

            var groups = response.Entries
                .Cast<SearchResultEntry>()
                .Select(MapToLdapGroup)
                .ToList();

            RecordOperation("SearchGroups", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "SearchGroups", "group", groups.Count, sw.ElapsedMilliseconds, true);

            return groups;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchGroups", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching groups with filter: {Filter}", searchFilter);
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
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
            await EnsureConnectedAsync(cancellationToken);

            const string filter = "(objectClass=group)";
            var result = await SearchGroupsPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("GetAllGroups", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "GetAllGroups", "group", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetAllGroups", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error getting all groups");
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
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
            await EnsureConnectedAsync(cancellationToken);

            var filter = BuildGroupLdapFilter(criteria);
            var result = await SearchGroupsPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("SearchGroups", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "SearchGroups", "group", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchGroups", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching groups with criteria");
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
        }
    }

    private async Task<LdapGroup?> SearchSingleGroupAsync(string filter, CancellationToken cancellationToken)
    {
        var searchRequest = new SearchRequest(
            GetBaseDn(),
            filter,
            SearchScope.Subtree,
            GetGroupAttributes());

        searchRequest.SizeLimit = 1;

        var response = await Task.Run(
            () => (SearchResponse)_connection!.SendRequest(searchRequest),
            cancellationToken);

        if (response.Entries.Count == 0)
        {
            return null;
        }

        return MapToLdapGroup((SearchResultEntry)response.Entries[0]!);
    }

    private async Task<PagedResult<LdapGroup>> SearchGroupsPagedAsync(string filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var skip = (page - 1) * pageSize;

        var countRequest = new SearchRequest(
            GetBaseDn(),
            filter,
            SearchScope.Subtree,
            "objectGUID");

        var countResponse = await Task.Run(
            () => (SearchResponse)_connection!.SendRequest(countRequest),
            cancellationToken);

        var totalCount = countResponse.Entries.Count;

        var searchRequest = new SearchRequest(
            GetBaseDn(),
            filter,
            SearchScope.Subtree,
            GetGroupAttributes());

        searchRequest.SizeLimit = skip + pageSize + 1;

        var response = await Task.Run(
            () => (SearchResponse)_connection!.SendRequest(searchRequest),
            cancellationToken);

        var allEntries = response.Entries.Cast<SearchResultEntry>().ToList();
        var pagedEntries = allEntries.Skip(skip).Take(pageSize).ToList();

        var groups = pagedEntries.Select(MapToLdapGroup).ToList();

        return PagedResult<LdapGroup>.Create(groups, page, pageSize, totalCount);
    }

    private static string BuildGroupLdapFilter(LdapGroupSearchCriteria criteria)
    {
        var filters = new List<string>
        {
            "(objectClass=group)"
        };

        if (!string.IsNullOrEmpty(criteria.Name))
            filters.Add($"(cn={EscapeLdapFilterWithWildcard(criteria.Name)})");

        if (!string.IsNullOrEmpty(criteria.DisplayName))
            filters.Add($"(displayName={EscapeLdapFilterWithWildcard(criteria.DisplayName)})");

        if (!string.IsNullOrEmpty(criteria.Description))
            filters.Add($"(description={EscapeLdapFilterWithWildcard(criteria.Description)})");

        if (!string.IsNullOrEmpty(criteria.Email))
            filters.Add($"(mail={EscapeLdapFilterWithWildcard(criteria.Email)})");

        if (criteria.IsSecurityGroup.HasValue)
        {
            // groupType bit 0x80000000 = security group
            filters.Add(criteria.IsSecurityGroup.Value
                ? "(groupType:1.2.840.113556.1.4.803:=2147483648)"
                : "(!(groupType:1.2.840.113556.1.4.803:=2147483648))");
        }

        if (!string.IsNullOrEmpty(criteria.GroupScope))
        {
            // DomainLocal = 4, Global = 2, Universal = 8
            var scopeValue = criteria.GroupScope.ToLowerInvariant() switch
            {
                "domainlocal" => "4",
                "global" => "2",
                "universal" => "8",
                _ => null
            };
            if (scopeValue != null)
            {
                filters.Add($"(groupType:1.2.840.113556.1.4.803:={scopeValue})");
            }
        }

        if (!string.IsNullOrEmpty(criteria.ManagedBy))
            filters.Add($"(managedBy={EscapeLdapFilter(criteria.ManagedBy)})");

        if (!string.IsNullOrEmpty(criteria.HasMember))
            filters.Add($"(member={EscapeLdapFilter(criteria.HasMember)})");

        if (!string.IsNullOrEmpty(criteria.MemberOfGroup))
            filters.Add($"(memberOf={EscapeLdapFilter(criteria.MemberOfGroup)})");

        if (criteria.CustomAttributes != null)
        {
            foreach (var (attr, value) in criteria.CustomAttributes)
            {
                filters.Add($"({EscapeLdapFilter(attr)}={EscapeLdapFilterWithWildcard(value)})");
            }
        }

        if (!string.IsNullOrEmpty(criteria.CustomFilter))
        {
            filters.Add(criteria.CustomFilter);
        }

        return $"(&{string.Join("", filters)})";
    }

    private LdapGroup MapToLdapGroup(SearchResultEntry entry)
    {
        var groupTypeValue = GetAttributeValue(entry, "groupType");
        var groupType = ParseGroupType(groupTypeValue);

        var group = new LdapGroup
        {
            DirectoryType = LdapDirectoryType.ActiveDirectory,
            Id = GetAttributeValue(entry, "objectGUID"),
            Name = GetAttributeValue(entry, "cn") ?? string.Empty,
            DistinguishedName = entry.DistinguishedName,
            DisplayName = GetAttributeValue(entry, "displayName"),
            Description = GetAttributeValue(entry, "description"),
            Email = GetAttributeValue(entry, "mail"),
            GroupType = groupType.Type,
            GroupScope = groupType.Scope,
            IsSecurityGroup = groupType.IsSecurity,
            IsMailEnabled = !string.IsNullOrEmpty(GetAttributeValue(entry, "mail")),
            ManagedBy = GetAttributeValue(entry, "managedBy"),
            CreatedAt = ParseDateTime(GetAttributeValue(entry, "whenCreated")),
            ModifiedAt = ParseDateTime(GetAttributeValue(entry, "whenChanged")),
            Members = GetMultiValueAttribute(entry, "member"),
            MemberOf = GetMultiValueAttribute(entry, "memberOf")
        };

        group.MemberCount = group.Members.Count;

        return group;
    }

    private static (string? Type, string? Scope, bool IsSecurity) ParseGroupType(string? groupTypeValue)
    {
        if (string.IsNullOrEmpty(groupTypeValue) || !int.TryParse(groupTypeValue, out var groupType))
        {
            return (null, null, false);
        }

        var isSecurity = (groupType & unchecked((int)0x80000000)) != 0;
        var type = isSecurity ? "Security" : "Distribution";

        string? scope = null;
        if ((groupType & 4) != 0) scope = "DomainLocal";
        else if ((groupType & 2) != 0) scope = "Global";
        else if ((groupType & 8) != 0) scope = "Universal";

        return (type, scope, isSecurity);
    }

    private static IList<string> GetMultiValueAttribute(SearchResultEntry entry, string attributeName)
    {
        if (!entry.Attributes.Contains(attributeName))
        {
            return [];
        }

        var values = entry.Attributes[attributeName].GetValues(typeof(string));
        return values.Cast<string>().ToList();
    }

    private static string[] GetGroupAttributes() =>
    [
        "objectGUID",
        "cn",
        "sAMAccountName",
        "distinguishedName",
        "displayName",
        "description",
        "mail",
        "groupType",
        "managedBy",
        "member",
        "memberOf",
        "whenCreated",
        "whenChanged"
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
            await EnsureConnectedAsync(cancellationToken);

            var escapedName = EscapeLdapFilter(computerName.TrimEnd('$'));
            var filter = $"(&(objectClass=computer)(|(cn={escapedName})(sAMAccountName={escapedName}$)(dNSHostName={escapedName}*)))";
            var computer = await SearchSingleComputerAsync(filter, cancellationToken);

            RecordOperation("GetComputerByName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByName", "computer", computer != null ? 1 : 0, sw.ElapsedMilliseconds, true);

            return computer;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetComputerByName", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching for computer: {ComputerName}", computerName);
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
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
            await EnsureConnectedAsync(cancellationToken);

            var searchRequest = new SearchRequest(
                distinguishedNameOrId,
                "(objectClass=computer)",
                SearchScope.Base,
                GetComputerAttributes());

            searchRequest.SizeLimit = 1;

            var response = await Task.Run(
                () => (SearchResponse)_connection!.SendRequest(searchRequest),
                cancellationToken);

            if (response.Entries.Count == 0)
            {
                RecordOperation("GetComputerByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByDistinguishedName", "computer", 0, sw.ElapsedMilliseconds, true);
                return null;
            }

            var computer = MapToLdapComputer((SearchResultEntry)response.Entries[0]!);

            RecordOperation("GetComputerByDistinguishedName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByDistinguishedName", "computer", 1, sw.ElapsedMilliseconds, true);

            return computer;
        }
        catch (LdapException ex) when (ex.ErrorCode == 32) // No such object
        {
            RecordOperation("GetComputerByDistinguishedName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByDistinguishedName", "computer", 0, sw.ElapsedMilliseconds, true);
            return null;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetComputerByDistinguishedName", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching for computer by DN: {DN}", distinguishedNameOrId);
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
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
            await EnsureConnectedAsync(cancellationToken);

            var searchRequest = new SearchRequest(
                GetBaseDn(),
                searchFilter,
                SearchScope.Subtree,
                GetComputerAttributes());

            searchRequest.SizeLimit = maxResults;

            var response = await Task.Run(
                () => (SearchResponse)_connection!.SendRequest(searchRequest),
                cancellationToken);

            var computers = response.Entries
                .Cast<SearchResultEntry>()
                .Select(MapToLdapComputer)
                .ToList();

            RecordOperation("SearchComputers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "SearchComputers", "computer", computers.Count, sw.ElapsedMilliseconds, true);

            return computers;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchComputers", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching computers with filter: {Filter}", searchFilter);
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
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
            await EnsureConnectedAsync(cancellationToken);

            const string filter = "(objectClass=computer)";
            var result = await SearchComputersPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("GetAllComputers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "GetAllComputers", "computer", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "GetAllComputers", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error getting all computers");
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
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
            await EnsureConnectedAsync(cancellationToken);

            var filter = BuildComputerLdapFilter(criteria);
            var result = await SearchComputersPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("SearchComputers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapPagedQuery(ServiceName, "SearchComputers", "computer", result.Items.Count, page, pageSize, sw.ElapsedMilliseconds);

            return result;
        }
        catch (LdapException ex)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "SearchComputers", ex.GetType().Name);
            _logger.LogError(ex, "LDAP error searching computers with criteria");
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
        }
    }

    private async Task<LdapComputer?> SearchSingleComputerAsync(string filter, CancellationToken cancellationToken)
    {
        var searchRequest = new SearchRequest(
            GetBaseDn(),
            filter,
            SearchScope.Subtree,
            GetComputerAttributes());

        searchRequest.SizeLimit = 1;

        var response = await Task.Run(
            () => (SearchResponse)_connection!.SendRequest(searchRequest),
            cancellationToken);

        if (response.Entries.Count == 0)
        {
            return null;
        }

        return MapToLdapComputer((SearchResultEntry)response.Entries[0]!);
    }

    private async Task<PagedResult<LdapComputer>> SearchComputersPagedAsync(string filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var skip = (page - 1) * pageSize;

        var countRequest = new SearchRequest(
            GetBaseDn(),
            filter,
            SearchScope.Subtree,
            "objectGUID");

        var countResponse = await Task.Run(
            () => (SearchResponse)_connection!.SendRequest(countRequest),
            cancellationToken);

        var totalCount = countResponse.Entries.Count;

        var searchRequest = new SearchRequest(
            GetBaseDn(),
            filter,
            SearchScope.Subtree,
            GetComputerAttributes());

        searchRequest.SizeLimit = skip + pageSize + 1;

        var response = await Task.Run(
            () => (SearchResponse)_connection!.SendRequest(searchRequest),
            cancellationToken);

        var allEntries = response.Entries.Cast<SearchResultEntry>().ToList();
        var pagedEntries = allEntries.Skip(skip).Take(pageSize).ToList();

        var computers = pagedEntries.Select(MapToLdapComputer).ToList();

        return PagedResult<LdapComputer>.Create(computers, page, pageSize, totalCount);
    }

    private static string BuildComputerLdapFilter(LdapComputerSearchCriteria criteria)
    {
        var filters = new List<string>
        {
            "(objectClass=computer)"
        };

        if (!string.IsNullOrEmpty(criteria.Name))
            filters.Add($"(cn={EscapeLdapFilterWithWildcard(criteria.Name)})");

        if (!string.IsNullOrEmpty(criteria.DisplayName))
            filters.Add($"(displayName={EscapeLdapFilterWithWildcard(criteria.DisplayName)})");

        if (!string.IsNullOrEmpty(criteria.Description))
            filters.Add($"(description={EscapeLdapFilterWithWildcard(criteria.Description)})");

        if (!string.IsNullOrEmpty(criteria.DnsHostName))
            filters.Add($"(dNSHostName={EscapeLdapFilterWithWildcard(criteria.DnsHostName)})");

        if (!string.IsNullOrEmpty(criteria.OperatingSystem))
            filters.Add($"(operatingSystem={EscapeLdapFilterWithWildcard(criteria.OperatingSystem)})");

        if (!string.IsNullOrEmpty(criteria.OperatingSystemVersion))
            filters.Add($"(operatingSystemVersion={EscapeLdapFilterWithWildcard(criteria.OperatingSystemVersion)})");

        if (!string.IsNullOrEmpty(criteria.Location))
            filters.Add($"(location={EscapeLdapFilterWithWildcard(criteria.Location)})");

        if (criteria.IsEnabled.HasValue)
        {
            // userAccountControl: 0x2 = ACCOUNTDISABLE
            filters.Add(criteria.IsEnabled.Value
                ? "(!(userAccountControl:1.2.840.113556.1.4.803:=2))"
                : "(userAccountControl:1.2.840.113556.1.4.803:=2)");
        }

        if (!string.IsNullOrEmpty(criteria.ManagedBy))
            filters.Add($"(managedBy={EscapeLdapFilter(criteria.ManagedBy)})");

        if (!string.IsNullOrEmpty(criteria.MemberOfGroup))
            filters.Add($"(memberOf={EscapeLdapFilter(criteria.MemberOfGroup)})");

        if (criteria.CustomAttributes != null)
        {
            foreach (var (attr, value) in criteria.CustomAttributes)
            {
                filters.Add($"({EscapeLdapFilter(attr)}={EscapeLdapFilterWithWildcard(value)})");
            }
        }

        if (!string.IsNullOrEmpty(criteria.CustomFilter))
        {
            filters.Add(criteria.CustomFilter);
        }

        return $"(&{string.Join("", filters)})";
    }

    private LdapComputer MapToLdapComputer(SearchResultEntry entry)
    {
        var computer = new LdapComputer
        {
            DirectoryType = LdapDirectoryType.ActiveDirectory,
            Id = GetAttributeValue(entry, "objectGUID"),
            Name = GetAttributeValue(entry, "cn") ?? string.Empty,
            DistinguishedName = entry.DistinguishedName,
            DisplayName = GetAttributeValue(entry, "displayName"),
            Description = GetAttributeValue(entry, "description"),
            DnsHostName = GetAttributeValue(entry, "dNSHostName"),
            SamAccountName = GetAttributeValue(entry, "sAMAccountName"),
            OperatingSystem = GetAttributeValue(entry, "operatingSystem"),
            OperatingSystemVersion = GetAttributeValue(entry, "operatingSystemVersion"),
            OperatingSystemServicePack = GetAttributeValue(entry, "operatingSystemServicePack"),
            Location = GetAttributeValue(entry, "location"),
            ManagedBy = GetAttributeValue(entry, "managedBy"),
            IsEnabled = !IsAccountDisabled(entry),
            CreatedAt = ParseDateTime(GetAttributeValue(entry, "whenCreated")),
            ModifiedAt = ParseDateTime(GetAttributeValue(entry, "whenChanged")),
            LastLogon = ParseFileTime(GetAttributeValue(entry, "lastLogon")),
            LastLogonTimestamp = ParseFileTime(GetAttributeValue(entry, "lastLogonTimestamp")),
            PasswordLastSet = ParseFileTime(GetAttributeValue(entry, "pwdLastSet")),
            MemberOf = GetMultiValueAttribute(entry, "memberOf"),
            ServicePrincipalNames = GetMultiValueAttribute(entry, "servicePrincipalName")
        };

        return computer;
    }

    private static string[] GetComputerAttributes() =>
    [
        "objectGUID",
        "cn",
        "sAMAccountName",
        "distinguishedName",
        "displayName",
        "description",
        "dNSHostName",
        "operatingSystem",
        "operatingSystemVersion",
        "operatingSystemServicePack",
        "location",
        "managedBy",
        "userAccountControl",
        "memberOf",
        "servicePrincipalName",
        "whenCreated",
        "whenChanged",
        "lastLogon",
        "lastLogonTimestamp",
        "pwdLastSet"
    ];

    #endregion

    #region Advanced Authentication Methods

    /// <inheritdoc />
    public LdapAuthenticationResult Authenticate(LdapAuthenticationOptions options)
    {
        return AuthenticateAsync(options).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
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
                LdapAuthenticationMode.Simple => await AuthenticateSimpleAsync(options, cancellationToken),
                LdapAuthenticationMode.Anonymous => await AuthenticateAnonymousAsync(options, cancellationToken),
                LdapAuthenticationMode.Kerberos => await AuthenticateKerberosInternalAsync(options, cancellationToken),
                LdapAuthenticationMode.Ntlm => await AuthenticateNtlmAsync(options, cancellationToken),
                LdapAuthenticationMode.Negotiate => await AuthenticateNegotiateAsync(options, cancellationToken),
                LdapAuthenticationMode.IntegratedWindows => await AuthenticateIntegratedWindowsAsync(options, cancellationToken),
                LdapAuthenticationMode.Certificate => await AuthenticateCertificateInternalAsync(options, cancellationToken),
                _ => throw new NotSupportedException($"Authentication mode {options.Mode} is not supported by Active Directory.")
            };

            RecordOperation("Authenticate", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, options.Mode.ToString(), result.IsAuthenticated);

            return result;
        }
        catch (Exception ex) when (ex is not NotSupportedException && ex is not InvalidOperationException && ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "Authenticate", ex.GetType().Name);
            _logger.LogError(ex, "Authentication failed with mode {Mode}", options.Mode);
            throw new InvalidOperationException($"Authentication failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<LdapAuthenticationResult> AuthenticateWithKerberosAsync(
        string? username = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var options = new LdapAuthenticationOptions
            {
                Mode = LdapAuthenticationMode.Kerberos,
                Username = username,
                Domain = _options.Domain
            };

            var result = await AuthenticateKerberosInternalAsync(options, cancellationToken);

            RecordOperation("AuthenticateWithKerberos", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "Kerberos", result.IsAuthenticated);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "AuthenticateWithKerberos", ex.GetType().Name);
            _logger.LogError(ex, "Kerberos authentication failed for user {Username}", username ?? "(current)");
            throw new InvalidOperationException($"Kerberos authentication failed: {ex.Message}", ex);
        }
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

            var result = await AuthenticateCertificateInternalAsync(options, cancellationToken);

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
            LdapAuthenticationMode.Simple,
            LdapAuthenticationMode.Anonymous,
            LdapAuthenticationMode.Kerberos,
            LdapAuthenticationMode.Ntlm,
            LdapAuthenticationMode.Negotiate,
            LdapAuthenticationMode.IntegratedWindows,
            LdapAuthenticationMode.Certificate
        ];
    }

    private async Task<LdapAuthenticationResult> AuthenticateSimpleAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        var server = _options.Server ?? _options.Domain;
        var identifier = new LdapDirectoryIdentifier(server, _options.Port);

        using var connection = new LdapConnection(identifier)
        {
            Timeout = options.Timeout
        };

        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = _options.UseSsl;
        connection.AuthType = AuthType.Basic;

        if (!_options.ValidateCertificate)
        {
            connection.SessionOptions.VerifyServerCertificate = (_, _) => true;
        }

        var credential = new NetworkCredential(options.Username, options.Password, options.Domain ?? _options.Domain);

        try
        {
            await Task.Run(() => connection.Bind(credential), cancellationToken);

            var result = LdapAuthenticationResult.Success(
                options.Username!,
                LdapAuthenticationMode.Simple,
                LdapDirectoryType.ActiveDirectory);

            if (options.IncludeGroups || options.IncludeClaims)
            {
                result = await EnrichAuthenticationResultAsync(result, options, cancellationToken);
            }

            _logger.LogDebug("Simple authentication succeeded for user: {Username}", options.Username);
            return result;
        }
        catch (LdapException ex) when (ex.ErrorCode == 49)
        {
            _logger.LogDebug("Invalid credentials for user: {Username}", options.Username);
            return LdapAuthenticationResult.Failure(
                "Invalid username or password.",
                "49",
                LdapAuthenticationMode.Simple,
                LdapDirectoryType.ActiveDirectory);
        }
    }

    private async Task<LdapAuthenticationResult> AuthenticateAnonymousAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        var server = _options.Server ?? _options.Domain;
        var identifier = new LdapDirectoryIdentifier(server, _options.Port);

        using var connection = new LdapConnection(identifier)
        {
            Timeout = options.Timeout
        };

        connection.SessionOptions.ProtocolVersion = 3;
        connection.AuthType = AuthType.Anonymous;

        await Task.Run(() => connection.Bind(), cancellationToken);

        _logger.LogDebug("Anonymous authentication succeeded");
        return LdapAuthenticationResult.Success(
            "anonymous",
            LdapAuthenticationMode.Anonymous,
            LdapDirectoryType.ActiveDirectory);
    }

    private async Task<LdapAuthenticationResult> AuthenticateKerberosInternalAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        var server = _options.Server ?? _options.Domain;
        var identifier = new LdapDirectoryIdentifier(server, _options.Port);

        using var connection = new LdapConnection(identifier)
        {
            Timeout = options.Timeout
        };

        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = _options.UseSsl;
        connection.AuthType = AuthType.Kerberos;

        if (!_options.ValidateCertificate)
        {
            connection.SessionOptions.VerifyServerCertificate = (_, _) => true;
        }

        try
        {
            if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
            {
                var credential = new NetworkCredential(options.Username, options.Password, options.Domain ?? _options.Domain);
                await Task.Run(() => connection.Bind(credential), cancellationToken);
            }
            else
            {
                // Use current Windows identity (integrated)
                await Task.Run(() => connection.Bind(), cancellationToken);
            }

            var username = options.Username ?? Environment.UserName;

            var result = LdapAuthenticationResult.Success(
                username,
                LdapAuthenticationMode.Kerberos,
                LdapDirectoryType.ActiveDirectory);

            if (options.IncludeGroups || options.IncludeClaims)
            {
                result = await EnrichAuthenticationResultAsync(result, options, cancellationToken);
            }

            _logger.LogDebug("Kerberos authentication succeeded for user: {Username}", username);
            return result;
        }
        catch (LdapException ex) when (ex.ErrorCode == 49)
        {
            _logger.LogDebug("Kerberos authentication failed: invalid credentials");
            return LdapAuthenticationResult.Failure(
                "Kerberos authentication failed: invalid credentials.",
                "49",
                LdapAuthenticationMode.Kerberos,
                LdapDirectoryType.ActiveDirectory);
        }
    }

    private async Task<LdapAuthenticationResult> AuthenticateNtlmAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        var server = _options.Server ?? _options.Domain;
        var identifier = new LdapDirectoryIdentifier(server, _options.Port);

        using var connection = new LdapConnection(identifier)
        {
            Timeout = options.Timeout
        };

        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = _options.UseSsl;
        connection.AuthType = AuthType.Ntlm;

        if (!_options.ValidateCertificate)
        {
            connection.SessionOptions.VerifyServerCertificate = (_, _) => true;
        }

        try
        {
            if (!string.IsNullOrEmpty(options.Username))
            {
                var credential = new NetworkCredential(options.Username, options.Password, options.Domain ?? _options.Domain);
                await Task.Run(() => connection.Bind(credential), cancellationToken);
            }
            else
            {
                await Task.Run(() => connection.Bind(), cancellationToken);
            }

            var username = options.Username ?? Environment.UserName;

            var result = LdapAuthenticationResult.Success(
                username,
                LdapAuthenticationMode.Ntlm,
                LdapDirectoryType.ActiveDirectory);

            if (options.IncludeGroups || options.IncludeClaims)
            {
                result = await EnrichAuthenticationResultAsync(result, options, cancellationToken);
            }

            _logger.LogDebug("NTLM authentication succeeded for user: {Username}", username);
            return result;
        }
        catch (LdapException ex) when (ex.ErrorCode == 49)
        {
            return LdapAuthenticationResult.Failure(
                "NTLM authentication failed: invalid credentials.",
                "49",
                LdapAuthenticationMode.Ntlm,
                LdapDirectoryType.ActiveDirectory);
        }
    }

    private async Task<LdapAuthenticationResult> AuthenticateNegotiateAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        var server = _options.Server ?? _options.Domain;
        var identifier = new LdapDirectoryIdentifier(server, _options.Port);

        using var connection = new LdapConnection(identifier)
        {
            Timeout = options.Timeout
        };

        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = _options.UseSsl;
        connection.AuthType = AuthType.Negotiate;

        if (!_options.ValidateCertificate)
        {
            connection.SessionOptions.VerifyServerCertificate = (_, _) => true;
        }

        try
        {
            if (!string.IsNullOrEmpty(options.Username))
            {
                var credential = new NetworkCredential(options.Username, options.Password, options.Domain ?? _options.Domain);
                await Task.Run(() => connection.Bind(credential), cancellationToken);
            }
            else
            {
                await Task.Run(() => connection.Bind(), cancellationToken);
            }

            var username = options.Username ?? Environment.UserName;

            var result = LdapAuthenticationResult.Success(
                username,
                LdapAuthenticationMode.Negotiate,
                LdapDirectoryType.ActiveDirectory);

            if (options.IncludeGroups || options.IncludeClaims)
            {
                result = await EnrichAuthenticationResultAsync(result, options, cancellationToken);
            }

            _logger.LogDebug("Negotiate authentication succeeded for user: {Username}", username);
            return result;
        }
        catch (LdapException ex) when (ex.ErrorCode == 49)
        {
            return LdapAuthenticationResult.Failure(
                "Negotiate authentication failed: invalid credentials.",
                "49",
                LdapAuthenticationMode.Negotiate,
                LdapDirectoryType.ActiveDirectory);
        }
    }

    private async Task<LdapAuthenticationResult> AuthenticateIntegratedWindowsAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        var server = _options.Server ?? _options.Domain;
        var identifier = new LdapDirectoryIdentifier(server, _options.Port);

        using var connection = new LdapConnection(identifier)
        {
            Timeout = options.Timeout
        };

        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = _options.UseSsl;
        connection.AuthType = AuthType.Negotiate; // Negotiate uses current Windows identity

        if (!_options.ValidateCertificate)
        {
            connection.SessionOptions.VerifyServerCertificate = (_, _) => true;
        }

        try
        {
            // Use current Windows identity
            await Task.Run(() => connection.Bind(), cancellationToken);

            var username = Environment.UserName;

            var result = LdapAuthenticationResult.Success(
                username,
                LdapAuthenticationMode.IntegratedWindows,
                LdapDirectoryType.ActiveDirectory);

            if (options.IncludeGroups || options.IncludeClaims)
            {
                result = await EnrichAuthenticationResultAsync(result, options, cancellationToken);
            }

            _logger.LogDebug("Integrated Windows authentication succeeded for user: {Username}", username);
            return result;
        }
        catch (LdapException ex)
        {
            return LdapAuthenticationResult.Failure(
                $"Integrated Windows authentication failed: {ex.Message}",
                ex.ErrorCode.ToString(),
                LdapAuthenticationMode.IntegratedWindows,
                LdapDirectoryType.ActiveDirectory);
        }
    }

    private Task<LdapAuthenticationResult> AuthenticateCertificateInternalAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        var certificate = options.GetCertificate();
        if (certificate == null)
        {
            throw new InvalidOperationException("Certificate is required for certificate authentication.");
        }

        // Note: System.DirectoryServices.Protocols client certificate support
        // is platform-dependent. On Windows, use the certificate store or
        // configure client certificates at the TLS layer.

        _logger.LogWarning(
            "Certificate authentication via System.DirectoryServices.Protocols requires " +
            "platform-specific configuration. Consider using Windows certificate store " +
            "or configuring TLS client certificates at the system level.");

        // For cross-platform support, we return a failure with guidance
        // The actual implementation would require Windows-specific APIs
        return Task.FromResult(LdapAuthenticationResult.Failure(
            "Certificate authentication requires platform-specific configuration. " +
            "On Windows, configure the client certificate in the certificate store and " +
            "use integrated Windows authentication with certificate mapping enabled on the AD server.",
            "NOT_IMPLEMENTED",
            LdapAuthenticationMode.Certificate,
            LdapDirectoryType.ActiveDirectory));
    }

    private static string ExtractUsernameFromCertificate(X509Certificate2 certificate)
    {
        // Try to extract username from subject CN
        var subject = certificate.Subject;
        var parts = subject.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(3);
            }
        }

        // Fallback to subject
        return subject;
    }

    private async Task<LdapAuthenticationResult> EnrichAuthenticationResultAsync(
        LdapAuthenticationResult result,
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        if (!result.IsAuthenticated || string.IsNullOrEmpty(result.Username))
        {
            return result;
        }

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            var user = await GetUserByUsernameAsync(result.Username, cancellationToken);
            if (user == null)
            {
                return result;
            }

            var groups = options.IncludeGroups ? user.Groups.ToList() : null;
            Dictionary<string, object>? claims = null;

            if (options.IncludeClaims)
            {
                claims = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(user.Email))
                    claims["email"] = user.Email;
                if (!string.IsNullOrEmpty(user.DisplayName))
                    claims["name"] = user.DisplayName;
                if (!string.IsNullOrEmpty(user.FirstName))
                    claims["given_name"] = user.FirstName;
                if (!string.IsNullOrEmpty(user.LastName))
                    claims["family_name"] = user.LastName;
                if (!string.IsNullOrEmpty(user.Department))
                    claims["department"] = user.Department;
                if (!string.IsNullOrEmpty(user.JobTitle))
                    claims["title"] = user.JobTitle;

                // Add requested custom attributes
                foreach (var attr in options.ClaimAttributes)
                {
                    if (user.CustomAttributes.TryGetValue(attr, out var value) && value != null)
                    {
                        claims[attr] = value;
                    }
                }
            }

            return new LdapAuthenticationResult
            {
                IsAuthenticated = true,
                Username = result.Username,
                UserDistinguishedName = user.DistinguishedName,
                UserId = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AuthenticationMode = result.AuthenticationMode,
                DirectoryType = LdapDirectoryType.ActiveDirectory,
                AuthenticatedAt = result.AuthenticatedAt,
                Groups = groups?.AsReadOnly(),
                Claims = claims?.AsReadOnly()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich authentication result for user: {Username}", result.Username);
            return result;
        }
    }

    #endregion

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            _connection?.Dispose();
            _connection = null;
            _isConnected = false;
        }
        finally
        {
            _connectionLock.Release();
        }

        _connectionLock.Dispose();
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_isConnected && _connection != null)
        {
            return;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected && _connection != null)
            {
                return;
            }

            var server = _options.Server ?? _options.Domain;
            var identifier = new LdapDirectoryIdentifier(server, _options.Port);

            _connection = new LdapConnection(identifier)
            {
                Timeout = _options.ConnectionTimeout
            };

            _connection.SessionOptions.ProtocolVersion = 3;
            _connection.SessionOptions.SecureSocketLayer = _options.UseSsl;
            _connection.SessionOptions.ReferralChasing = _options.FollowReferrals
                ? ReferralChasingOptions.All
                : ReferralChasingOptions.None;

            if (!_options.ValidateCertificate)
            {
                _connection.SessionOptions.VerifyServerCertificate = (_, _) => true;
            }

            if (_options.UseCurrentCredentials)
            {
                await Task.Run(() => _connection.Bind(), cancellationToken);
            }
            else if (!string.IsNullOrEmpty(_options.Username))
            {
                var credential = new NetworkCredential(_options.Username, _options.Password, _options.Domain);
                await Task.Run(() => _connection.Bind(credential), cancellationToken);
            }
            else
            {
                await Task.Run(() => _connection.Bind(), cancellationToken);
            }

            _isConnected = true;
            ToolboxMeter.RecordLdapConnection(ServiceName, "ActiveDirectory", server, true);
            _logger.LogDebug("Connected to Active Directory: {Server}", server);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapConnection(ServiceName, "ActiveDirectory", _options.Server ?? _options.Domain, false);
            ToolboxMeter.RecordLdapError(ServiceName, "Connect", ex.GetType().Name);
            _logger.LogError(ex, "Failed to connect to Active Directory");
            throw new InvalidOperationException($"Failed to connect to Active Directory: {ex.Message}", ex);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<LdapUser?> SearchSingleUserAsync(string filter, CancellationToken cancellationToken)
    {
        var searchRequest = new SearchRequest(
            GetBaseDn(),
            filter,
            SearchScope.Subtree,
            GetUserAttributes());

        searchRequest.SizeLimit = 1;

        var response = await Task.Run(
            () => (SearchResponse)_connection!.SendRequest(searchRequest),
            cancellationToken);

        if (response.Entries.Count == 0)
        {
            return null;
        }

        return MapToLdapUser((SearchResultEntry)response.Entries[0]!);
    }

    private LdapUser MapToLdapUser(SearchResultEntry entry)
    {
        var user = new LdapUser
        {
            DirectoryType = LdapDirectoryType.ActiveDirectory,
            Id = GetAttributeValue(entry, "objectGUID"),
            Username = GetAttributeValue(entry, "sAMAccountName") ?? string.Empty,
            UserPrincipalName = GetAttributeValue(entry, "userPrincipalName"),
            DistinguishedName = entry.DistinguishedName,
            DisplayName = GetAttributeValue(entry, "displayName"),
            FirstName = GetAttributeValue(entry, "givenName"),
            LastName = GetAttributeValue(entry, "sn"),
            Email = GetAttributeValue(entry, "mail"),
            PhoneNumber = GetAttributeValue(entry, "telephoneNumber"),
            MobilePhone = GetAttributeValue(entry, "mobile"),
            JobTitle = GetAttributeValue(entry, "title"),
            Department = GetAttributeValue(entry, "department"),
            Company = GetAttributeValue(entry, "company"),
            Office = GetAttributeValue(entry, "physicalDeliveryOfficeName"),
            Manager = GetAttributeValue(entry, "manager"),
            StreetAddress = GetAttributeValue(entry, "streetAddress"),
            City = GetAttributeValue(entry, "l"),
            State = GetAttributeValue(entry, "st"),
            PostalCode = GetAttributeValue(entry, "postalCode"),
            Country = GetAttributeValue(entry, "co"),
            IsEnabled = !IsAccountDisabled(entry),
            IsLockedOut = IsAccountLockedOut(entry),
            CreatedAt = ParseDateTime(GetAttributeValue(entry, "whenCreated")),
            ModifiedAt = ParseDateTime(GetAttributeValue(entry, "whenChanged")),
            LastLogon = ParseFileTime(GetAttributeValue(entry, "lastLogon")),
            Groups = GetGroupMembership(entry)
        };

        // Add custom attributes
        foreach (var attrName in _options.CustomAttributes)
        {
            var value = GetAttributeValue(entry, attrName);
            if (value != null)
            {
                user.CustomAttributes[attrName] = value;
            }
        }

        return user;
    }

    private static string? GetAttributeValue(SearchResultEntry entry, string attributeName)
    {
        if (!entry.Attributes.Contains(attributeName))
        {
            return null;
        }

        var values = entry.Attributes[attributeName].GetValues(typeof(string));
        return values.Length > 0 ? values[0] as string : null;
    }

    private static bool IsAccountDisabled(SearchResultEntry entry)
    {
        var uacValue = GetAttributeValue(entry, "userAccountControl");
        if (uacValue == null || !int.TryParse(uacValue, out var uac))
        {
            return false;
        }

        // 0x2 = ACCOUNTDISABLE
        return (uac & 0x2) != 0;
    }

    private static bool IsAccountLockedOut(SearchResultEntry entry)
    {
        var lockoutTime = GetAttributeValue(entry, "lockoutTime");
        return lockoutTime != null && lockoutTime != "0";
    }

    private static IList<string> GetGroupMembership(SearchResultEntry entry)
    {
        if (!entry.Attributes.Contains("memberOf"))
        {
            return [];
        }

        var values = entry.Attributes["memberOf"].GetValues(typeof(string));
        return values.Cast<string>().ToList();
    }

    private static DateTimeOffset? ParseDateTime(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        // AD format: "yyyyMMddHHmmss.0Z"
        if (DateTime.TryParseExact(
                value.Split('.')[0],
                "yyyyMMddHHmmss",
                null,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var dt))
        {
            return new DateTimeOffset(dt, TimeSpan.Zero);
        }

        return null;
    }

    private static DateTimeOffset? ParseFileTime(string? value)
    {
        if (string.IsNullOrEmpty(value) || value == "0")
        {
            return null;
        }

        if (long.TryParse(value, out var fileTime) && fileTime > 0)
        {
            try
            {
                return DateTimeOffset.FromFileTime(fileTime);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private string GetBaseDn()
    {
        if (!string.IsNullOrEmpty(_options.BaseDn))
        {
            return _options.BaseDn;
        }

        // Generate from domain
        var parts = _options.Domain.Split('.');
        return string.Join(",", parts.Select(p => $"DC={p}"));
    }

    private static string EscapeLdapFilter(string value)
    {
        return value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }

    private static string[] GetUserAttributes() =>
    [
        "objectGUID",
        "sAMAccountName",
        "userPrincipalName",
        "distinguishedName",
        "displayName",
        "givenName",
        "sn",
        "mail",
        "telephoneNumber",
        "mobile",
        "title",
        "department",
        "company",
        "physicalDeliveryOfficeName",
        "manager",
        "streetAddress",
        "l",
        "st",
        "postalCode",
        "co",
        "userAccountControl",
        "lockoutTime",
        "memberOf",
        "whenCreated",
        "whenChanged",
        "lastLogon"
    ];
}
