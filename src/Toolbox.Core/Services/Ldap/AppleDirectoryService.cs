// @file AppleDirectoryService.cs
// @brief Apple Directory Services implementation
// @details Implements ILdapService for macOS Open Directory
// @note Uses Novell.Directory.Ldap.NETStandard with Apple-specific attributes

using Microsoft.Extensions.Options;
using Novell.Directory.Ldap;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Ldap;

/// <summary>
/// LDAP service implementation for Apple Directory Services (Open Directory).
/// </summary>
/// <remarks>
/// <para>
/// This service provides access to Apple Open Directory servers
/// (typically running on macOS Server) using the LDAP protocol
/// with Apple-specific object classes and attributes.
/// </para>
/// <para>
/// Features:
/// </para>
/// <list type="bullet">
///   <item><description>User lookup by username (uid) or email</description></item>
///   <item><description>Custom LDAP filter search</description></item>
///   <item><description>Credential validation via bind</description></item>
///   <item><description>Group membership retrieval</description></item>
///   <item><description>SSL/TLS support</description></item>
/// </list>
/// </remarks>
/// <seealso cref="ILdapService"/>
public sealed class AppleDirectoryService : BaseAsyncDisposableService, ILdapService
{
    private readonly AppleDirectoryOptions _options;
    private readonly ILogger<AppleDirectoryService> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private LdapConnection? _connection;
    private bool _isConnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppleDirectoryService"/> class.
    /// </summary>
    /// <param name="options">The Apple Directory options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when host or baseDn is empty.</exception>
    public AppleDirectoryService(
        IOptions<AppleDirectoryOptions> options,
        ILogger<AppleDirectoryService> logger)
        : base("AppleDirectoryService", logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new ArgumentException("Host cannot be empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(_options.BaseDn))
        {
            throw new ArgumentException("BaseDn cannot be empty.", nameof(options));
        }

        _logger.LogDebug(
            "AppleDirectoryService initialized for {Host}:{Port}",
            _options.Host,
            _options.Port);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppleDirectoryService"/> class.
    /// </summary>
    /// <param name="options">The Apple Directory options.</param>
    /// <param name="logger">The logger instance.</param>
    public AppleDirectoryService(
        AppleDirectoryOptions options,
        ILogger<AppleDirectoryService> logger)
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
            ToolboxMeter.RecordLdapQuery(ServiceName, "GetUserByUsername", user != null);

            return user;
        }
        catch (LdapException ex)
        {
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
            ToolboxMeter.RecordLdapQuery(ServiceName, "GetUserByEmail", user != null);

            return user;
        }
        catch (LdapException ex)
        {
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

            var searchConstraints = new LdapSearchConstraints
            {
                MaxResults = maxResults,
                TimeLimit = (int)_options.OperationTimeout.TotalMilliseconds
            };

            var results = await Task.Run(
                () => _connection!.Search(
                    _options.BaseDn,
                    LdapConnection.ScopeSub,
                    searchFilter,
                    GetUserAttributes(),
                    false,
                    searchConstraints),
                cancellationToken);

            var users = new List<LdapUser>();
            while (results.HasMore() && users.Count < maxResults)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var entry = results.Next();
                    users.Add(MapToLdapUser(entry));
                }
                catch (LdapReferralException)
                {
                    // Skip referrals
                }
            }

            RecordOperation("SearchUsers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQuery(ServiceName, "SearchUsers", true);

            return users;
        }
        catch (LdapException ex)
        {
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
            // First find the user to get their DN
            var user = await GetUserByUsernameAsync(username, cancellationToken);
            if (user?.DistinguishedName == null)
            {
                _logger.LogDebug("User not found for credential validation: {Username}", username);
                return false;
            }

            // Try to bind with user credentials
            using var connection = new LdapConnection();

            if (_options.UseSsl)
            {
                connection.SecureSocketLayer = true;
            }

            await Task.Run(() => connection.Connect(_options.Host, _options.Port), cancellationToken);
            await Task.Run(() => connection.Bind(user.DistinguishedName, password), cancellationToken);

            RecordOperation("ValidateCredentials", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQuery(ServiceName, "ValidateCredentials", true);

            _logger.LogDebug("Credentials validated for user: {Username}", username);
            return true;
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            _logger.LogDebug("Invalid credentials for user: {Username}", username);
            RecordOperation("ValidateCredentials", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQuery(ServiceName, "ValidateCredentials", false);
            return false;
        }
        catch (LdapException ex)
        {
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

            var filter = $"(objectClass={_options.UserObjectClass})";
            var result = await SearchUsersPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("GetAllUsers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQuery(ServiceName, "GetAllUsers", true);

            return result;
        }
        catch (LdapException ex)
        {
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
            ToolboxMeter.RecordLdapQuery(ServiceName, "SearchUsers", true);

            return result;
        }
        catch (LdapException ex)
        {
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

            var escapedGroup = EscapeLdapFilter(groupDnOrName);
            var filter = $"(&(objectClass={_options.UserObjectClass})({_options.GroupMembershipAttribute}={escapedGroup}))";

            var result = await SearchUsersPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("GetGroupMembers", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQuery(ServiceName, "GetGroupMembers", true);

            return result;
        }
        catch (LdapException ex)
        {
            _logger.LogError(ex, "LDAP error getting group members: {Group}", groupDnOrName);
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
        }
    }

    private async Task<PagedResult<LdapUser>> SearchUsersPagedAsync(string filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var skip = (page - 1) * pageSize;

        var searchConstraints = new LdapSearchConstraints
        {
            MaxResults = 0,
            TimeLimit = (int)_options.OperationTimeout.TotalMilliseconds
        };

        var results = await Task.Run(
            () => _connection!.Search(
                _options.BaseDn,
                LdapConnection.ScopeSub,
                filter,
                GetUserAttributes(),
                false,
                searchConstraints),
            cancellationToken);

        var allUsers = new List<LdapUser>();
        while (results.HasMore())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var entry = results.Next();
                allUsers.Add(MapToLdapUser(entry));
            }
            catch (LdapReferralException)
            {
                // Skip referrals
            }
        }

        var totalCount = allUsers.Count;
        var pagedUsers = allUsers.Skip(skip).Take(pageSize).ToList();

        return PagedResult<LdapUser>.Create(pagedUsers, page, pageSize, totalCount);
    }

    private string BuildLdapFilter(LdapSearchCriteria criteria)
    {
        var filters = new List<string>
        {
            $"(objectClass={_options.UserObjectClass})"
        };

        if (!string.IsNullOrEmpty(criteria.Username))
            filters.Add($"({_options.UsernameAttribute}={EscapeLdapFilterWithWildcard(criteria.Username)})");

        if (!string.IsNullOrEmpty(criteria.DisplayName))
            filters.Add($"({_options.DisplayNameAttribute}={EscapeLdapFilterWithWildcard(criteria.DisplayName)})");

        if (!string.IsNullOrEmpty(criteria.FirstName))
            filters.Add($"({_options.FirstNameAttribute}={EscapeLdapFilterWithWildcard(criteria.FirstName)})");

        if (!string.IsNullOrEmpty(criteria.LastName))
            filters.Add($"({_options.LastNameAttribute}={EscapeLdapFilterWithWildcard(criteria.LastName)})");

        if (!string.IsNullOrEmpty(criteria.Email))
            filters.Add($"({_options.EmailAttribute}={EscapeLdapFilterWithWildcard(criteria.Email)})");

        if (!string.IsNullOrEmpty(criteria.Department))
            filters.Add($"(departmentNumber={EscapeLdapFilter(criteria.Department)})");

        if (!string.IsNullOrEmpty(criteria.JobTitle))
            filters.Add($"(title={EscapeLdapFilterWithWildcard(criteria.JobTitle)})");

        if (!string.IsNullOrEmpty(criteria.Company))
            filters.Add($"(o={EscapeLdapFilter(criteria.Company)})");

        if (!string.IsNullOrEmpty(criteria.City))
            filters.Add($"(l={EscapeLdapFilter(criteria.City)})");

        if (!string.IsNullOrEmpty(criteria.Country))
            filters.Add($"(c={EscapeLdapFilter(criteria.Country)})");

        if (!string.IsNullOrEmpty(criteria.MemberOfGroup))
            filters.Add($"({_options.GroupMembershipAttribute}={EscapeLdapFilter(criteria.MemberOfGroup)})");

        if (criteria.MemberOfAnyGroup?.Count > 0)
        {
            var groupFilters = criteria.MemberOfAnyGroup
                .Select(g => $"({_options.GroupMembershipAttribute}={EscapeLdapFilter(g)})")
                .ToList();
            filters.Add($"(|{string.Join("", groupFilters)})");
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
            var filter = $"(&(objectClass={_options.GroupObjectClass})(|(cn={escapedName})(displayName={escapedName})))";
            var group = await SearchSingleGroupAsync(filter, cancellationToken);

            RecordOperation("GetGroupByName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQuery(ServiceName, "GetGroupByName", group != null);

            return group;
        }
        catch (LdapException ex)
        {
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

            var searchConstraints = new LdapSearchConstraints
            {
                MaxResults = 1,
                TimeLimit = (int)_options.OperationTimeout.TotalMilliseconds
            };

            var results = await Task.Run(
                () => _connection!.Search(
                    distinguishedNameOrId,
                    LdapConnection.ScopeBase,
                    $"(objectClass={_options.GroupObjectClass})",
                    GetGroupAttributes(),
                    false,
                    searchConstraints),
                cancellationToken);

            if (!results.HasMore())
            {
                RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQuery(ServiceName, "GetGroupByDistinguishedName", false);
                return null;
            }

            try
            {
                var entry = results.Next();
                var group = MapToLdapGroup(entry);
                RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQuery(ServiceName, "GetGroupByDistinguishedName", true);
                return group;
            }
            catch (LdapReferralException)
            {
                RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQuery(ServiceName, "GetGroupByDistinguishedName", false);
                return null;
            }
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.NoSuchObject)
        {
            RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQuery(ServiceName, "GetGroupByDistinguishedName", false);
            return null;
        }
        catch (LdapException ex)
        {
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

            var searchConstraints = new LdapSearchConstraints
            {
                MaxResults = maxResults,
                TimeLimit = (int)_options.OperationTimeout.TotalMilliseconds
            };

            var results = await Task.Run(
                () => _connection!.Search(
                    _options.BaseDn,
                    LdapConnection.ScopeSub,
                    searchFilter,
                    GetGroupAttributes(),
                    false,
                    searchConstraints),
                cancellationToken);

            var groups = new List<LdapGroup>();
            while (results.HasMore() && groups.Count < maxResults)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var entry = results.Next();
                    groups.Add(MapToLdapGroup(entry));
                }
                catch (LdapReferralException)
                {
                    // Skip referrals
                }
            }

            RecordOperation("SearchGroups", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQuery(ServiceName, "SearchGroups", true);

            return groups;
        }
        catch (LdapException ex)
        {
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

            var filter = $"(objectClass={_options.GroupObjectClass})";
            var result = await SearchGroupsPagedAsync(filter, page, pageSize, cancellationToken);

            RecordOperation("GetAllGroups", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQuery(ServiceName, "GetAllGroups", true);

            return result;
        }
        catch (LdapException ex)
        {
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
            ToolboxMeter.RecordLdapQuery(ServiceName, "SearchGroups", true);

            return result;
        }
        catch (LdapException ex)
        {
            _logger.LogError(ex, "LDAP error searching groups with criteria");
            throw new InvalidOperationException($"LDAP query failed: {ex.Message}", ex);
        }
    }

    private async Task<LdapGroup?> SearchSingleGroupAsync(string filter, CancellationToken cancellationToken)
    {
        var searchConstraints = new LdapSearchConstraints
        {
            MaxResults = 1,
            TimeLimit = (int)_options.OperationTimeout.TotalMilliseconds
        };

        var results = await Task.Run(
            () => _connection!.Search(
                _options.BaseDn,
                LdapConnection.ScopeSub,
                filter,
                GetGroupAttributes(),
                false,
                searchConstraints),
            cancellationToken);

        if (!results.HasMore())
        {
            return null;
        }

        try
        {
            var entry = results.Next();
            return MapToLdapGroup(entry);
        }
        catch (LdapReferralException)
        {
            return null;
        }
    }

    private async Task<PagedResult<LdapGroup>> SearchGroupsPagedAsync(string filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var skip = (page - 1) * pageSize;

        var searchConstraints = new LdapSearchConstraints
        {
            MaxResults = 0,
            TimeLimit = (int)_options.OperationTimeout.TotalMilliseconds
        };

        var results = await Task.Run(
            () => _connection!.Search(
                _options.BaseDn,
                LdapConnection.ScopeSub,
                filter,
                GetGroupAttributes(),
                false,
                searchConstraints),
            cancellationToken);

        var allGroups = new List<LdapGroup>();
        while (results.HasMore())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var entry = results.Next();
                allGroups.Add(MapToLdapGroup(entry));
            }
            catch (LdapReferralException)
            {
                // Skip referrals
            }
        }

        var totalCount = allGroups.Count;
        var pagedGroups = allGroups.Skip(skip).Take(pageSize).ToList();

        return PagedResult<LdapGroup>.Create(pagedGroups, page, pageSize, totalCount);
    }

    private string BuildGroupLdapFilter(LdapGroupSearchCriteria criteria)
    {
        var filters = new List<string>
        {
            $"(objectClass={_options.GroupObjectClass})"
        };

        if (!string.IsNullOrEmpty(criteria.Name))
            filters.Add($"(cn={EscapeLdapFilterWithWildcard(criteria.Name)})");

        if (!string.IsNullOrEmpty(criteria.DisplayName))
            filters.Add($"(displayName={EscapeLdapFilterWithWildcard(criteria.DisplayName)})");

        if (!string.IsNullOrEmpty(criteria.Description))
            filters.Add($"(description={EscapeLdapFilterWithWildcard(criteria.Description)})");

        if (!string.IsNullOrEmpty(criteria.Email))
            filters.Add($"(mail={EscapeLdapFilterWithWildcard(criteria.Email)})");

        if (!string.IsNullOrEmpty(criteria.HasMember))
            filters.Add($"({_options.GroupMemberAttribute}={EscapeLdapFilter(criteria.HasMember)})");

        if (!string.IsNullOrEmpty(criteria.MemberOfGroup))
            filters.Add($"({_options.GroupMembershipAttribute}={EscapeLdapFilter(criteria.MemberOfGroup)})");

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

    private LdapGroup MapToLdapGroup(LdapEntry entry)
    {
        var group = new LdapGroup
        {
            DirectoryType = LdapDirectoryType.AppleDirectory,
            Id = GetAttributeValue(entry, _options.UniqueIdAttribute),
            Name = GetAttributeValue(entry, "cn") ?? string.Empty,
            DistinguishedName = entry.Dn,
            DisplayName = GetAttributeValue(entry, "displayName") ?? GetAttributeValue(entry, "cn"),
            Description = GetAttributeValue(entry, "description"),
            Email = GetAttributeValue(entry, "mail"),
            IsMailEnabled = !string.IsNullOrEmpty(GetAttributeValue(entry, "mail")),
            Members = GetMultiValueAttribute(entry, _options.GroupMemberAttribute),
            MemberOf = GetMultiValueAttribute(entry, _options.GroupMembershipAttribute)
        };

        group.MemberCount = group.Members.Count;

        return group;
    }

    private static IList<string> GetMultiValueAttribute(LdapEntry entry, string attributeName)
    {
        try
        {
            var attr = entry.GetAttribute(attributeName);
            if (attr == null)
            {
                return [];
            }

            return attr.StringValueArray.ToList();
        }
        catch
        {
            return [];
        }
    }

    private string[] GetGroupAttributes() =>
    [
        _options.UniqueIdAttribute,
        "cn",
        "displayName",
        "description",
        "mail",
        _options.GroupMemberAttribute,
        _options.GroupMembershipAttribute
    ];

    #endregion

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection != null)
            {
                if (_connection.Connected)
                {
                    _connection.Disconnect();
                }
                _connection.Dispose();
                _connection = null;
            }
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
        if (_isConnected && _connection?.Connected == true)
        {
            return;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected && _connection?.Connected == true)
            {
                return;
            }

            _connection?.Dispose();
            _connection = new LdapConnection
            {
                ConnectionTimeout = (int)_options.ConnectionTimeout.TotalMilliseconds
            };

            if (_options.UseSsl)
            {
                _connection.SecureSocketLayer = true;
            }

            await Task.Run(() => _connection.Connect(_options.Host, _options.Port), cancellationToken);

            if (!string.IsNullOrEmpty(_options.BindDn))
            {
                await Task.Run(
                    () => _connection.Bind(_options.BindDn, _options.BindPassword ?? string.Empty),
                    cancellationToken);
            }
            else
            {
                await Task.Run(() => _connection.Bind(null, null), cancellationToken);
            }

            _isConnected = true;
            _logger.LogDebug("Connected to Apple Directory: {Host}:{Port}", _options.Host, _options.Port);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to Apple Directory");
            throw new InvalidOperationException($"Failed to connect to Apple Directory: {ex.Message}", ex);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<LdapUser?> SearchSingleUserAsync(string filter, CancellationToken cancellationToken)
    {
        var searchConstraints = new LdapSearchConstraints
        {
            MaxResults = 1,
            TimeLimit = (int)_options.OperationTimeout.TotalMilliseconds
        };

        var results = await Task.Run(
            () => _connection!.Search(
                _options.BaseDn,
                LdapConnection.ScopeSub,
                filter,
                GetUserAttributes(),
                false,
                searchConstraints),
            cancellationToken);

        if (!results.HasMore())
        {
            return null;
        }

        try
        {
            var entry = results.Next();
            return MapToLdapUser(entry);
        }
        catch (LdapReferralException)
        {
            return null;
        }
    }

    private LdapUser MapToLdapUser(LdapEntry entry)
    {
        var user = new LdapUser
        {
            DirectoryType = LdapDirectoryType.AppleDirectory,
            Id = GetAttributeValue(entry, _options.UniqueIdAttribute),
            Username = GetAttributeValue(entry, _options.UsernameAttribute) ?? string.Empty,
            DistinguishedName = entry.Dn,
            DisplayName = GetAttributeValue(entry, _options.DisplayNameAttribute),
            FirstName = GetAttributeValue(entry, _options.FirstNameAttribute),
            LastName = GetAttributeValue(entry, _options.LastNameAttribute),
            Email = GetAttributeValue(entry, _options.EmailAttribute),
            PhoneNumber = GetAttributeValue(entry, "telephoneNumber"),
            MobilePhone = GetAttributeValue(entry, "mobile"),
            JobTitle = GetAttributeValue(entry, "title"),
            Department = GetAttributeValue(entry, "departmentNumber") ?? GetAttributeValue(entry, "ou"),
            Company = GetAttributeValue(entry, "o"),
            Office = GetAttributeValue(entry, "physicalDeliveryOfficeName"),
            Manager = GetAttributeValue(entry, "manager"),
            StreetAddress = GetAttributeValue(entry, "street") ?? GetAttributeValue(entry, "postalAddress"),
            City = GetAttributeValue(entry, "l"),
            State = GetAttributeValue(entry, "st"),
            PostalCode = GetAttributeValue(entry, "postalCode"),
            Country = GetAttributeValue(entry, "c"),
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

    private static string? GetAttributeValue(LdapEntry entry, string attributeName)
    {
        try
        {
            var attr = entry.GetAttribute(attributeName);
            return attr?.StringValue;
        }
        catch
        {
            return null;
        }
    }

    private IList<string> GetGroupMembership(LdapEntry entry)
    {
        try
        {
            var attr = entry.GetAttribute(_options.GroupMembershipAttribute);
            if (attr == null)
            {
                return [];
            }

            return attr.StringValueArray.ToList();
        }
        catch
        {
            return [];
        }
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

    private string[] GetUserAttributes()
    {
        var attrs = new List<string>
        {
            _options.UniqueIdAttribute,
            _options.UsernameAttribute,
            _options.DisplayNameAttribute,
            _options.FirstNameAttribute,
            _options.LastNameAttribute,
            _options.EmailAttribute,
            _options.GroupMembershipAttribute,
            "telephoneNumber",
            "mobile",
            "title",
            "departmentNumber",
            "ou",
            "o",
            "physicalDeliveryOfficeName",
            "manager",
            "street",
            "postalAddress",
            "l",
            "st",
            "postalCode",
            "c"
        };

        attrs.AddRange(_options.CustomAttributes);

        return attrs.Distinct().ToArray();
    }
}
