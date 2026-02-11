// @file AppleDirectoryService.cs
// @brief Apple Directory Services implementation
// @details Implements ILdapService for macOS Open Directory
// @note Uses Novell.Directory.Ldap.NETStandard with Apple-specific attributes

using System.Security.Cryptography.X509Certificates;
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
    /// <summary>
    /// The Apple Directory configuration options.
    /// </summary>
    private readonly AppleDirectoryOptions _options;

    /// <summary>
    /// The logger instance for diagnostic output.
    /// </summary>
    private readonly ILogger<AppleDirectoryService> _logger;

    /// <summary>
    /// Semaphore for thread-safe connection management.
    /// </summary>
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    /// <summary>
    /// The active LDAP connection to the Apple Directory server.
    /// </summary>
    private LdapConnection? _connection;

    /// <summary>
    /// Indicates whether the service is currently connected to the server.
    /// </summary>
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
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "AppleDirectory", true);

            _logger.LogDebug("Credentials validated for user: {Username}", username);
            return true;
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            _logger.LogDebug("Invalid credentials for user: {Username}", username);
            RecordOperation("ValidateCredentials", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "AppleDirectory", false);
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
            ToolboxMeter.RecordLdapError(ServiceName, "GetUserGroups", ex.GetType().Name);
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

            var escapedGroup = EscapeLdapFilter(groupDnOrName);
            var filter = $"(&(objectClass={_options.UserObjectClass})({_options.GroupMembershipAttribute}={escapedGroup}))";

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

    /// <summary>
    /// Searches for users matching the specified LDAP filter with pagination.
    /// </summary>
    /// <param name="filter">The LDAP filter to apply.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged result containing the matching users.</returns>
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

    /// <summary>
    /// Builds an LDAP filter string from the specified search criteria.
    /// </summary>
    /// <param name="criteria">The search criteria to convert to an LDAP filter.</param>
    /// <returns>An LDAP filter string combining all specified criteria with AND logic.</returns>
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

    /// <summary>
    /// Escapes special characters in a value for use in an LDAP filter, preserving wildcards.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value with wildcards (*) preserved.</returns>
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
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByDistinguishedName", "group", 0, sw.ElapsedMilliseconds, true);
                return null;
            }

            try
            {
                var entry = results.Next();
                var group = MapToLdapGroup(entry);
                RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByDistinguishedName", "group", 1, sw.ElapsedMilliseconds, true);
                return group;
            }
            catch (LdapReferralException)
            {
                RecordOperation("GetGroupByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetGroupByDistinguishedName", "group", 0, sw.ElapsedMilliseconds, true);
                return null;
            }
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.NoSuchObject)
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

            var filter = $"(objectClass={_options.GroupObjectClass})";
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

    /// <summary>
    /// Searches for a single group matching the specified LDAP filter.
    /// </summary>
    /// <param name="filter">The LDAP filter to apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching group, or null if not found.</returns>
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

    /// <summary>
    /// Searches for groups matching the specified LDAP filter with pagination.
    /// </summary>
    /// <param name="filter">The LDAP filter to apply.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged result containing the matching groups.</returns>
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

    /// <summary>
    /// Builds an LDAP filter string from the specified group search criteria.
    /// </summary>
    /// <param name="criteria">The search criteria to convert to an LDAP filter.</param>
    /// <returns>An LDAP filter string combining all specified criteria with AND logic.</returns>
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

    /// <summary>
    /// Maps an LDAP entry to an LdapGroup object.
    /// </summary>
    /// <param name="entry">The LDAP entry from the search result.</param>
    /// <returns>A populated LdapGroup object.</returns>
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

    /// <summary>
    /// Gets the values of a multi-valued attribute from an LDAP entry.
    /// </summary>
    /// <param name="entry">The LDAP entry.</param>
    /// <param name="attributeName">The name of the attribute to retrieve.</param>
    /// <returns>A list of attribute values, or an empty list if the attribute is not present.</returns>
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

    /// <summary>
    /// Gets the list of group attributes to retrieve in search operations.
    /// </summary>
    /// <returns>An array of attribute names for group searches.</returns>
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

            var escapedName = EscapeLdapFilter(computerName);
            var filter = $"(&(objectClass={_options.ComputerObjectClass})(|(cn={escapedName})(displayName={escapedName})))";
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

            var searchConstraints = new LdapSearchConstraints
            {
                MaxResults = 1,
                TimeLimit = (int)_options.OperationTimeout.TotalMilliseconds
            };

            var results = await Task.Run(
                () => _connection!.Search(
                    distinguishedNameOrId,
                    LdapConnection.ScopeBase,
                    $"(objectClass={_options.ComputerObjectClass})",
                    GetComputerAttributes(),
                    false,
                    searchConstraints),
                cancellationToken);

            if (!results.HasMore())
            {
                RecordOperation("GetComputerByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByDistinguishedName", "computer", 0, sw.ElapsedMilliseconds, true);
                return null;
            }

            try
            {
                var entry = results.Next();
                var computer = MapToLdapComputer(entry);
                RecordOperation("GetComputerByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByDistinguishedName", "computer", 1, sw.ElapsedMilliseconds, true);
                return computer;
            }
            catch (LdapReferralException)
            {
                RecordOperation("GetComputerByDistinguishedName", sw.ElapsedMilliseconds);
                ToolboxMeter.RecordLdapQueryDetailed(ServiceName, "GetComputerByDistinguishedName", "computer", 0, sw.ElapsedMilliseconds, true);
                return null;
            }
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.NoSuchObject)
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
                    GetComputerAttributes(),
                    false,
                    searchConstraints),
                cancellationToken);

            var computers = new List<LdapComputer>();
            while (results.HasMore() && computers.Count < maxResults)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var entry = results.Next();
                    computers.Add(MapToLdapComputer(entry));
                }
                catch (LdapReferralException)
                {
                    // Skip referrals
                }
            }

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

            var filter = $"(objectClass={_options.ComputerObjectClass})";
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

    /// <summary>
    /// Searches for a single computer matching the specified LDAP filter.
    /// </summary>
    /// <param name="filter">The LDAP filter to apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching computer, or null if not found.</returns>
    private async Task<LdapComputer?> SearchSingleComputerAsync(string filter, CancellationToken cancellationToken)
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
                GetComputerAttributes(),
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
            return MapToLdapComputer(entry);
        }
        catch (LdapReferralException)
        {
            return null;
        }
    }

    /// <summary>
    /// Searches for computers matching the specified LDAP filter with pagination.
    /// </summary>
    /// <param name="filter">The LDAP filter to apply.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged result containing the matching computers.</returns>
    private async Task<PagedResult<LdapComputer>> SearchComputersPagedAsync(string filter, int page, int pageSize, CancellationToken cancellationToken)
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
                GetComputerAttributes(),
                false,
                searchConstraints),
            cancellationToken);

        var allComputers = new List<LdapComputer>();
        while (results.HasMore())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var entry = results.Next();
                allComputers.Add(MapToLdapComputer(entry));
            }
            catch (LdapReferralException)
            {
                // Skip referrals
            }
        }

        var totalCount = allComputers.Count;
        var pagedComputers = allComputers.Skip(skip).Take(pageSize).ToList();

        return PagedResult<LdapComputer>.Create(pagedComputers, page, pageSize, totalCount);
    }

    /// <summary>
    /// Builds an LDAP filter string from the specified computer search criteria.
    /// </summary>
    /// <param name="criteria">The search criteria to convert to an LDAP filter.</param>
    /// <returns>An LDAP filter string combining all specified criteria with AND logic.</returns>
    private string BuildComputerLdapFilter(LdapComputerSearchCriteria criteria)
    {
        var filters = new List<string>
        {
            $"(objectClass={_options.ComputerObjectClass})"
        };

        if (!string.IsNullOrEmpty(criteria.Name))
            filters.Add($"(cn={EscapeLdapFilterWithWildcard(criteria.Name)})");

        if (!string.IsNullOrEmpty(criteria.DisplayName))
            filters.Add($"(displayName={EscapeLdapFilterWithWildcard(criteria.DisplayName)})");

        if (!string.IsNullOrEmpty(criteria.Description))
            filters.Add($"(description={EscapeLdapFilterWithWildcard(criteria.Description)})");

        if (!string.IsNullOrEmpty(criteria.Location))
            filters.Add($"(l={EscapeLdapFilterWithWildcard(criteria.Location)})");

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

    /// <summary>
    /// Maps an LDAP entry to an LdapComputer object.
    /// </summary>
    /// <param name="entry">The LDAP entry from the search result.</param>
    /// <returns>A populated LdapComputer object.</returns>
    private LdapComputer MapToLdapComputer(LdapEntry entry)
    {
        var computer = new LdapComputer
        {
            DirectoryType = LdapDirectoryType.AppleDirectory,
            Id = GetAttributeValue(entry, _options.UniqueIdAttribute),
            Name = GetAttributeValue(entry, "cn") ?? string.Empty,
            DistinguishedName = entry.Dn,
            DisplayName = GetAttributeValue(entry, "displayName") ?? GetAttributeValue(entry, "cn"),
            Description = GetAttributeValue(entry, "description"),
            Location = GetAttributeValue(entry, "l"),
            MemberOf = GetMultiValueAttribute(entry, _options.GroupMembershipAttribute)
        };

        return computer;
    }

    /// <summary>
    /// Gets the list of computer attributes to retrieve in search operations.
    /// </summary>
    /// <returns>An array of attribute names for computer searches.</returns>
    private string[] GetComputerAttributes() =>
    [
        _options.UniqueIdAttribute,
        "cn",
        "displayName",
        "description",
        "l",
        _options.GroupMembershipAttribute
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
                LdapAuthenticationMode.SaslPlain => await AuthenticateSaslPlainAsync(options, cancellationToken),
                LdapAuthenticationMode.Certificate => await AuthenticateCertificateInternalAsync(options, cancellationToken),
                _ => throw new NotSupportedException($"Authentication mode {options.Mode} is not supported by Apple Directory.")
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
    /// <exception cref="NotSupportedException">
    /// Kerberos is not directly supported by Apple Directory through this library.
    /// Use native macOS authentication mechanisms instead.
    /// </exception>
    public Task<LdapAuthenticationResult> AuthenticateWithKerberosAsync(
        string? username = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Kerberos authentication is not directly supported by Apple Directory through this library. " +
            "Use macOS native authentication mechanisms instead.");
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
            LdapAuthenticationMode.SaslPlain,
            LdapAuthenticationMode.Certificate
        ];
    }

    /// <summary>
    /// Authenticates a user using simple bind (username/password).
    /// </summary>
    /// <param name="options">The authentication options containing credentials.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authentication result indicating success or failure.</returns>
    private async Task<LdapAuthenticationResult> AuthenticateSimpleAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        using var connection = new LdapConnection
        {
            ConnectionTimeout = (int)options.Timeout.TotalMilliseconds
        };

        if (_options.UseSsl)
        {
            connection.SecureSocketLayer = true;
        }

        try
        {
            await Task.Run(() => connection.Connect(_options.Host, _options.Port), cancellationToken);

            var bindDn = BuildBindDn(options.Username!);
            await Task.Run(() => connection.Bind(bindDn, options.Password ?? string.Empty), cancellationToken);

            var result = LdapAuthenticationResult.Success(
                options.Username!,
                LdapAuthenticationMode.Simple,
                LdapDirectoryType.AppleDirectory);

            if (options.IncludeGroups || options.IncludeClaims)
            {
                result = await EnrichAuthenticationResultAsync(result, options, cancellationToken);
            }

            _logger.LogDebug("Simple authentication succeeded for user: {Username}", options.Username);
            return result;
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            _logger.LogDebug("Invalid credentials for user: {Username}", options.Username);
            return LdapAuthenticationResult.Failure(
                "Invalid username or password.",
                ex.ResultCode.ToString(),
                LdapAuthenticationMode.Simple,
                LdapDirectoryType.AppleDirectory);
        }
        finally
        {
            if (connection.Connected)
            {
                connection.Disconnect();
            }
        }
    }

    /// <summary>
    /// Authenticates using anonymous bind.
    /// </summary>
    /// <param name="options">The authentication options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authentication result.</returns>
    private async Task<LdapAuthenticationResult> AuthenticateAnonymousAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        using var connection = new LdapConnection
        {
            ConnectionTimeout = (int)options.Timeout.TotalMilliseconds
        };

        await Task.Run(() => connection.Connect(_options.Host, _options.Port), cancellationToken);
        await Task.Run(() => connection.Bind(null, null), cancellationToken);

        _logger.LogDebug("Anonymous authentication succeeded");
        return LdapAuthenticationResult.Success(
            "anonymous",
            LdapAuthenticationMode.Anonymous,
            LdapDirectoryType.AppleDirectory);
    }

    /// <summary>
    /// Authenticates a user using SASL PLAIN mechanism.
    /// </summary>
    /// <param name="options">The authentication options containing credentials.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authentication result indicating success or failure.</returns>
    /// <remarks>
    /// SASL PLAIN is similar to simple bind but uses the SASL framework.
    /// </remarks>
    private async Task<LdapAuthenticationResult> AuthenticateSaslPlainAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        using var connection = new LdapConnection
        {
            ConnectionTimeout = (int)options.Timeout.TotalMilliseconds
        };

        if (_options.UseSsl)
        {
            connection.SecureSocketLayer = true;
        }

        try
        {
            await Task.Run(() => connection.Connect(_options.Host, _options.Port), cancellationToken);

            var bindDn = BuildBindDn(options.Username!);
            await Task.Run(() => connection.Bind(bindDn, options.Password ?? string.Empty), cancellationToken);

            var result = LdapAuthenticationResult.Success(
                options.Username!,
                LdapAuthenticationMode.SaslPlain,
                LdapDirectoryType.AppleDirectory);

            if (options.IncludeGroups || options.IncludeClaims)
            {
                result = await EnrichAuthenticationResultAsync(result, options, cancellationToken);
            }

            _logger.LogDebug("SASL PLAIN authentication succeeded for user: {Username}", options.Username);
            return result;
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            return LdapAuthenticationResult.Failure(
                "SASL PLAIN authentication failed: invalid credentials.",
                ex.ResultCode.ToString(),
                LdapAuthenticationMode.SaslPlain,
                LdapDirectoryType.AppleDirectory);
        }
        finally
        {
            if (connection.Connected)
            {
                connection.Disconnect();
            }
        }
    }

    /// <summary>
    /// Authenticates using client certificate.
    /// </summary>
    /// <param name="options">The authentication options containing the certificate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authentication result.</returns>
    /// <remarks>
    /// Certificate authentication requires TLS client certificate configuration.
    /// This is currently not fully supported by the Novell.Directory.Ldap library.
    /// </remarks>
    private Task<LdapAuthenticationResult> AuthenticateCertificateInternalAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        var certificate = options.GetCertificate();
        if (certificate == null)
        {
            throw new InvalidOperationException("Certificate is required for certificate authentication.");
        }

        _logger.LogWarning(
            "Certificate authentication requires proper TLS client certificate configuration on the Open Directory server. " +
            "The Novell.Directory.Ldap library has limited support for this mechanism.");

        return Task.FromResult(LdapAuthenticationResult.Failure(
            "Certificate authentication is not fully supported by the Novell.Directory.Ldap library. " +
            "Configure TLS client certificate authentication at the server level.",
            "NOT_SUPPORTED",
            LdapAuthenticationMode.Certificate,
            LdapDirectoryType.AppleDirectory));
    }

    /// <summary>
    /// Builds a distinguished name for binding from a username.
    /// </summary>
    /// <param name="username">The username to convert to a DN.</param>
    /// <returns>The bind DN for the user.</returns>
    /// <remarks>
    /// If the username already contains an equals sign, it is assumed to be a DN and returned as-is.
    /// Otherwise, a DN is constructed using the configured username attribute and base DN.
    /// </remarks>
    private string BuildBindDn(string username)
    {
        if (username.Contains('='))
        {
            return username;
        }

        return $"{_options.UsernameAttribute}={EscapeLdapDn(username)},{_options.BaseDn}";
    }

    /// <summary>
    /// Escapes special characters in a value for use in a distinguished name.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value safe for use in a DN.</returns>
    private static string EscapeLdapDn(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(",", "\\,")
            .Replace("+", "\\+")
            .Replace("\"", "\\\"")
            .Replace("<", "\\<")
            .Replace(">", "\\>")
            .Replace(";", "\\;");
    }

    /// <summary>
    /// Enriches an authentication result with additional user information.
    /// </summary>
    /// <param name="result">The initial authentication result.</param>
    /// <param name="options">The authentication options specifying what information to include.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An enriched authentication result with additional user data.</returns>
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
                DirectoryType = LdapDirectoryType.AppleDirectory,
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

    #region Account Management Implementation

    /// <inheritdoc />
    public LdapManagementResult EnableAccount(LdapAccountOptions options)
    {
        return LdapManagementResult.NotSupported(LdapManagementOperation.EnableAccount, LdapDirectoryType.AppleDirectory);
    }

    /// <inheritdoc />
    public Task<LdapManagementResult> EnableAccountAsync(LdapAccountOptions options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LdapManagementResult.NotSupported(LdapManagementOperation.EnableAccount, LdapDirectoryType.AppleDirectory));
    }

    /// <inheritdoc />
    public LdapManagementResult DisableAccount(LdapAccountOptions options)
    {
        return LdapManagementResult.NotSupported(LdapManagementOperation.DisableAccount, LdapDirectoryType.AppleDirectory);
    }

    /// <inheritdoc />
    public Task<LdapManagementResult> DisableAccountAsync(LdapAccountOptions options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LdapManagementResult.NotSupported(LdapManagementOperation.DisableAccount, LdapDirectoryType.AppleDirectory));
    }

    /// <inheritdoc />
    public LdapManagementResult UnlockAccount(LdapAccountOptions options)
    {
        return LdapManagementResult.NotSupported(LdapManagementOperation.UnlockAccount, LdapDirectoryType.AppleDirectory);
    }

    /// <inheritdoc />
    public Task<LdapManagementResult> UnlockAccountAsync(LdapAccountOptions options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LdapManagementResult.NotSupported(LdapManagementOperation.UnlockAccount, LdapDirectoryType.AppleDirectory));
    }

    /// <inheritdoc />
    public LdapManagementResult SetAccountExpiration(LdapAccountOptions options)
    {
        return LdapManagementResult.NotSupported(LdapManagementOperation.SetAccountExpiration, LdapDirectoryType.AppleDirectory);
    }

    /// <inheritdoc />
    public Task<LdapManagementResult> SetAccountExpirationAsync(LdapAccountOptions options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LdapManagementResult.NotSupported(LdapManagementOperation.SetAccountExpiration, LdapDirectoryType.AppleDirectory));
    }

    #endregion

    #region Group Membership Implementation

    /// <inheritdoc />
    public LdapManagementResult AddToGroup(LdapGroupMembershipOptions options)
    {
        return AddToGroupAsync(options).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapManagementResult> AddToGroupAsync(
        LdapGroupMembershipOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            var groupDn = await ResolveGroupDistinguishedNameAsync(options, cancellationToken);
            if (string.IsNullOrEmpty(groupDn))
            {
                return LdapManagementResult.Failure(
                    LdapManagementOperation.AddToGroup,
                    "Group not found.");
            }

            var memberDn = await ResolveMemberDistinguishedNameAsync(options, cancellationToken);
            if (string.IsNullOrEmpty(memberDn))
            {
                return LdapManagementResult.Failure(
                    LdapManagementOperation.AddToGroup,
                    "Member not found.");
            }

            var memberAttr = new LdapAttribute("memberUid", memberDn);
            var mod = new LdapModification(LdapModification.Add, memberAttr);
            await Task.Run(() => _connection!.Modify(groupDn, mod), cancellationToken);

            _logger.LogInformation("Added {MemberDn} to group {GroupDn}", memberDn, groupDn);
            ToolboxMeter.RecordLdapManagement(ServiceName, "AddToGroup", true);

            return LdapManagementResult.Success(
                LdapManagementOperation.AddToGroup,
                groupDn,
                $"Member added to group.");
        }
        catch (LdapException ex) when (ex.ResultCode == 20)
        {
            return LdapManagementResult.Success(
                LdapManagementOperation.AddToGroup,
                details: "Member already exists in group.");
        }
        catch (LdapException ex)
        {
            _logger.LogError(ex, "Failed to add member to group");
            ToolboxMeter.RecordLdapManagement(ServiceName, "AddToGroup", false);
            return LdapManagementResult.Failure(
                LdapManagementOperation.AddToGroup,
                ex.Message,
                ex.ResultCode);
        }
    }

    /// <inheritdoc />
    public LdapGroupMembershipBatchResult AddToGroupBatch(LdapGroupMembershipOptions options)
    {
        return AddToGroupBatchAsync(options).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapGroupMembershipBatchResult> AddToGroupBatchAsync(
        LdapGroupMembershipOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var results = new List<LdapManagementResult>();
        var memberDns = options.GetAllMemberDns().ToList();

        foreach (var memberDn in memberDns)
        {
            var singleOptions = LdapGroupMembershipOptions.Create()
                .ForGroupDn(options.GroupDistinguishedName ?? string.Empty)
                .ForGroup(options.GroupName ?? string.Empty)
                .WithMemberDn(memberDn);

            var result = await AddToGroupAsync(singleOptions, cancellationToken);
            results.Add(result);

            if (!result.IsSuccess && !options.ContinueOnError)
            {
                break;
            }
        }

        return new LdapGroupMembershipBatchResult
        {
            TotalCount = memberDns.Count,
            SuccessCount = results.Count(r => r.IsSuccess),
            FailureCount = results.Count(r => !r.IsSuccess),
            Results = results
        };
    }

    /// <inheritdoc />
    public LdapManagementResult RemoveFromGroup(LdapGroupMembershipOptions options)
    {
        return RemoveFromGroupAsync(options).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapManagementResult> RemoveFromGroupAsync(
        LdapGroupMembershipOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            var groupDn = await ResolveGroupDistinguishedNameAsync(options, cancellationToken);
            if (string.IsNullOrEmpty(groupDn))
            {
                return LdapManagementResult.Failure(
                    LdapManagementOperation.RemoveFromGroup,
                    "Group not found.");
            }

            var memberDn = await ResolveMemberDistinguishedNameAsync(options, cancellationToken);
            if (string.IsNullOrEmpty(memberDn))
            {
                return LdapManagementResult.Failure(
                    LdapManagementOperation.RemoveFromGroup,
                    "Member not found.");
            }

            var memberAttr = new LdapAttribute("memberUid", memberDn);
            var mod = new LdapModification(LdapModification.Delete, memberAttr);
            await Task.Run(() => _connection!.Modify(groupDn, mod), cancellationToken);

            _logger.LogInformation("Removed {MemberDn} from group {GroupDn}", memberDn, groupDn);
            ToolboxMeter.RecordLdapManagement(ServiceName, "RemoveFromGroup", true);

            return LdapManagementResult.Success(
                LdapManagementOperation.RemoveFromGroup,
                groupDn,
                $"Member removed from group.");
        }
        catch (LdapException ex) when (ex.ResultCode == 16)
        {
            return LdapManagementResult.Success(
                LdapManagementOperation.RemoveFromGroup,
                details: "Member was not in group.");
        }
        catch (LdapException ex)
        {
            _logger.LogError(ex, "Failed to remove member from group");
            ToolboxMeter.RecordLdapManagement(ServiceName, "RemoveFromGroup", false);
            return LdapManagementResult.Failure(
                LdapManagementOperation.RemoveFromGroup,
                ex.Message,
                ex.ResultCode);
        }
    }

    /// <inheritdoc />
    public LdapGroupMembershipBatchResult RemoveFromGroupBatch(LdapGroupMembershipOptions options)
    {
        return RemoveFromGroupBatchAsync(options).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapGroupMembershipBatchResult> RemoveFromGroupBatchAsync(
        LdapGroupMembershipOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var results = new List<LdapManagementResult>();
        var memberDns = options.GetAllMemberDns().ToList();

        foreach (var memberDn in memberDns)
        {
            var singleOptions = LdapGroupMembershipOptions.Create()
                .ForGroupDn(options.GroupDistinguishedName ?? string.Empty)
                .ForGroup(options.GroupName ?? string.Empty)
                .WithMemberDn(memberDn);

            var result = await RemoveFromGroupAsync(singleOptions, cancellationToken);
            results.Add(result);

            if (!result.IsSuccess && !options.ContinueOnError)
            {
                break;
            }
        }

        return new LdapGroupMembershipBatchResult
        {
            TotalCount = memberDns.Count,
            SuccessCount = results.Count(r => r.IsSuccess),
            FailureCount = results.Count(r => !r.IsSuccess),
            Results = results
        };
    }

    #endregion

    #region Object Movement Implementation

    /// <inheritdoc />
    public LdapManagementResult MoveObject(LdapMoveOptions options)
    {
        return LdapManagementResult.NotSupported(LdapManagementOperation.MoveObject, LdapDirectoryType.AppleDirectory);
    }

    /// <inheritdoc />
    public Task<LdapManagementResult> MoveObjectAsync(LdapMoveOptions options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LdapManagementResult.NotSupported(LdapManagementOperation.MoveObject, LdapDirectoryType.AppleDirectory));
    }

    /// <inheritdoc />
    public LdapManagementResult RenameObject(string distinguishedName, string newCommonName)
    {
        return LdapManagementResult.NotSupported(LdapManagementOperation.RenameObject, LdapDirectoryType.AppleDirectory);
    }

    /// <inheritdoc />
    public Task<LdapManagementResult> RenameObjectAsync(string distinguishedName, string newCommonName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LdapManagementResult.NotSupported(LdapManagementOperation.RenameObject, LdapDirectoryType.AppleDirectory));
    }

    #endregion

    #region Password Management Implementation

    /// <inheritdoc />
    public LdapManagementResult ChangePassword(LdapPasswordOptions options)
    {
        return ChangePasswordAsync(options).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapManagementResult> ChangePasswordAsync(
        LdapPasswordOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);
        options.ValidateForChange();
        cancellationToken.ThrowIfCancellationRequested();

        if (options.IsAdministrativeReset)
        {
            return await ResetPasswordAsync(options, cancellationToken);
        }

        try
        {
            await EnsureConnectedAsync(cancellationToken);
            var dn = await ResolveUserDistinguishedNameAsync(options, cancellationToken);

            if (string.IsNullOrEmpty(dn))
            {
                return LdapManagementResult.Failure(
                    LdapManagementOperation.ChangePassword,
                    "User not found.");
            }

            var deletePasswordAttr = new LdapAttribute("userPassword", options.CurrentPassword!);
            var deleteMod = new LdapModification(LdapModification.Delete, deletePasswordAttr);
            var addPasswordAttr = new LdapAttribute("userPassword", options.NewPassword!);
            var addMod = new LdapModification(LdapModification.Add, addPasswordAttr);
            await Task.Run(() => _connection!.Modify(dn, new[] { deleteMod, addMod }), cancellationToken);

            _logger.LogInformation("Changed password for user: {DistinguishedName}", dn);
            ToolboxMeter.RecordLdapManagement(ServiceName, "ChangePassword", true);

            return LdapManagementResult.Success(
                LdapManagementOperation.ChangePassword,
                dn,
                "Password changed successfully.");
        }
        catch (LdapException ex)
        {
            _logger.LogError(ex, "Failed to change password");
            ToolboxMeter.RecordLdapManagement(ServiceName, "ChangePassword", false);
            return LdapManagementResult.Failure(
                LdapManagementOperation.ChangePassword,
                ex.Message,
                ex.ResultCode);
        }
    }

    /// <inheritdoc />
    public LdapManagementResult ResetPassword(LdapPasswordOptions options)
    {
        return ResetPasswordAsync(options).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<LdapManagementResult> ResetPasswordAsync(
        LdapPasswordOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(options.NewPassword))
        {
            throw new InvalidOperationException("NewPassword is required for password reset.");
        }

        try
        {
            await EnsureConnectedAsync(cancellationToken);
            var dn = await ResolveUserDistinguishedNameAsync(options, cancellationToken);

            if (string.IsNullOrEmpty(dn))
            {
                return LdapManagementResult.Failure(
                    LdapManagementOperation.ResetPassword,
                    "User not found.");
            }

            var passwordAttr = new LdapAttribute("userPassword", options.NewPassword);
            var passwordMod = new LdapModification(LdapModification.Replace, passwordAttr);
            await Task.Run(() => _connection!.Modify(dn, passwordMod), cancellationToken);

            _logger.LogInformation("Reset password for user: {DistinguishedName}", dn);
            ToolboxMeter.RecordLdapManagement(ServiceName, "ResetPassword", true);

            return LdapManagementResult.Success(
                LdapManagementOperation.ResetPassword,
                dn,
                "Password reset successfully.");
        }
        catch (LdapException ex)
        {
            _logger.LogError(ex, "Failed to reset password");
            ToolboxMeter.RecordLdapManagement(ServiceName, "ResetPassword", false);
            return LdapManagementResult.Failure(
                LdapManagementOperation.ResetPassword,
                ex.Message,
                ex.ResultCode);
        }
    }

    /// <inheritdoc />
    public LdapManagementResult ForcePasswordChangeAtNextLogon(LdapAccountOptions options)
    {
        return LdapManagementResult.NotSupported(LdapManagementOperation.ForcePasswordChange, LdapDirectoryType.AppleDirectory);
    }

    /// <inheritdoc />
    public Task<LdapManagementResult> ForcePasswordChangeAtNextLogonAsync(LdapAccountOptions options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LdapManagementResult.NotSupported(LdapManagementOperation.ForcePasswordChange, LdapDirectoryType.AppleDirectory));
    }

    /// <inheritdoc />
    public LdapManagementResult SetPasswordNeverExpires(LdapAccountOptions options, bool neverExpires = true)
    {
        return LdapManagementResult.NotSupported(LdapManagementOperation.SetPasswordNeverExpires, LdapDirectoryType.AppleDirectory);
    }

    /// <inheritdoc />
    public Task<LdapManagementResult> SetPasswordNeverExpiresAsync(LdapAccountOptions options, bool neverExpires = true, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LdapManagementResult.NotSupported(LdapManagementOperation.SetPasswordNeverExpires, LdapDirectoryType.AppleDirectory));
    }

    #endregion

    #region Management Capability Implementation

    /// <inheritdoc />
    public IReadOnlyList<LdapManagementOperation> GetSupportedManagementOperations()
    {
        return
        [
            LdapManagementOperation.AddToGroup,
            LdapManagementOperation.RemoveFromGroup,
            LdapManagementOperation.ChangePassword,
            LdapManagementOperation.ResetPassword
        ];
    }

    #endregion

    #region Management Helper Methods

    /// <summary>
    /// Resolves the distinguished name of a group from group membership options.
    /// </summary>
    /// <param name="options">The group membership options containing either DN or group name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The distinguished name, or null if the group was not found.</returns>
    private async Task<string?> ResolveGroupDistinguishedNameAsync(
        LdapGroupMembershipOptions options,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(options.GroupDistinguishedName))
        {
            return options.GroupDistinguishedName;
        }

        if (string.IsNullOrEmpty(options.GroupName))
        {
            return null;
        }

        var filter = $"(&(objectClass=posixGroup)(cn={EscapeLdapFilter(options.GroupName)}))";
        var searchConstraints = new LdapSearchConstraints { MaxResults = 1 };

        var results = await Task.Run(
            () => _connection!.Search(
                _options.BaseDn,
                LdapConnection.ScopeSub,
                filter,
                new[] { "dn" },
                false,
                searchConstraints),
            cancellationToken);

        return results.HasMore() ? results.Next().Dn : null;
    }

    /// <summary>
    /// Resolves the distinguished name of a member from group membership options.
    /// </summary>
    /// <param name="options">The group membership options containing either DN or member username.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The distinguished name, or null if the member was not found.</returns>
    private async Task<string?> ResolveMemberDistinguishedNameAsync(
        LdapGroupMembershipOptions options,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(options.MemberDistinguishedName))
        {
            return options.MemberDistinguishedName;
        }

        if (string.IsNullOrEmpty(options.MemberUsername))
        {
            return null;
        }

        var filter = $"(uid={EscapeLdapFilter(options.MemberUsername)})";
        var searchConstraints = new LdapSearchConstraints { MaxResults = 1 };

        var results = await Task.Run(
            () => _connection!.Search(
                _options.BaseDn,
                LdapConnection.ScopeSub,
                filter,
                new[] { "dn" },
                false,
                searchConstraints),
            cancellationToken);

        return results.HasMore() ? results.Next().Dn : null;
    }

    /// <summary>
    /// Resolves the distinguished name from password options.
    /// </summary>
    /// <param name="options">The password options containing either DN or username.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The distinguished name, or null if the user was not found.</returns>
    private async Task<string?> ResolveUserDistinguishedNameAsync(
        LdapPasswordOptions options,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(options.DistinguishedName))
        {
            return options.DistinguishedName;
        }

        if (string.IsNullOrEmpty(options.Username))
        {
            return null;
        }

        var filter = $"(uid={EscapeLdapFilter(options.Username)})";
        var searchConstraints = new LdapSearchConstraints { MaxResults = 1 };

        var results = await Task.Run(
            () => _connection!.Search(
                _options.BaseDn,
                LdapConnection.ScopeSub,
                filter,
                new[] { "dn" },
                false,
                searchConstraints),
            cancellationToken);

        return results.HasMore() ? results.Next().Dn : null;
    }

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

    /// <summary>
    /// Ensures that a connection to Apple Directory is established.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the connection fails.</exception>
    /// <remarks>
    /// This method uses a semaphore to ensure thread-safe connection establishment.
    /// If already connected, returns immediately. Otherwise, establishes a new connection
    /// using the configured options (host, port, SSL, bind credentials).
    /// </remarks>
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
            ToolboxMeter.RecordLdapConnection(ServiceName, "AppleDirectory", _options.Host, true);
            _logger.LogDebug("Connected to Apple Directory: {Host}:{Port}", _options.Host, _options.Port);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapConnection(ServiceName, "AppleDirectory", _options.Host, false);
            ToolboxMeter.RecordLdapError(ServiceName, "EnsureConnectedAsync", ex.GetType().Name);
            _logger.LogError(ex, "Failed to connect to Apple Directory");
            throw new InvalidOperationException($"Failed to connect to Apple Directory: {ex.Message}", ex);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Searches for a single user matching the specified LDAP filter.
    /// </summary>
    /// <param name="filter">The LDAP filter to apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching user, or null if not found.</returns>
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

    /// <summary>
    /// Maps an LDAP entry to an LdapUser object.
    /// </summary>
    /// <param name="entry">The LDAP entry from the search result.</param>
    /// <returns>A populated LdapUser object.</returns>
    /// <remarks>
    /// Maps Apple Directory attributes to LdapUser properties,
    /// including contact information, organizational data, and custom attributes.
    /// </remarks>
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

    /// <summary>
    /// Gets the string value of an attribute from an LDAP entry.
    /// </summary>
    /// <param name="entry">The LDAP entry.</param>
    /// <param name="attributeName">The name of the attribute to retrieve.</param>
    /// <returns>The attribute value as a string, or null if the attribute is not present.</returns>
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

    /// <summary>
    /// Gets the list of groups that the user is a member of.
    /// </summary>
    /// <param name="entry">The LDAP entry containing the group membership attribute.</param>
    /// <returns>A list of group identifiers, or an empty list if none.</returns>
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

    /// <summary>
    /// Escapes special characters in a value for use in an LDAP filter.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value safe for use in an LDAP filter.</returns>
    /// <remarks>
    /// Escapes the following characters according to RFC 4515:
    /// backslash, asterisk, parentheses, and null character.
    /// </remarks>
    private static string EscapeLdapFilter(string value)
    {
        return value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }

    /// <summary>
    /// Gets the list of user attributes to retrieve in search operations.
    /// </summary>
    /// <returns>An array of attribute names for user searches.</returns>
    /// <remarks>
    /// Returns Apple Directory user attributes including identity,
    /// contact information, organizational data, and configured custom attributes.
    /// </remarks>
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
