// @file OpenLdapService.cs
// @brief OpenLDAP service implementation
// @details Implements ILdapService for OpenLDAP and compatible directories
// @note Uses Novell.Directory.Ldap.NETStandard for cross-platform support

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Novell.Directory.Ldap;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Ldap;

/// <summary>
/// LDAP service implementation for OpenLDAP and compatible directories.
/// </summary>
/// <remarks>
/// <para>
/// This service provides access to OpenLDAP servers and compatible
/// LDAP directories (FreeIPA, 389 Directory Server, etc.) using
/// the Novell.Directory.Ldap library.
/// </para>
/// <para>
/// Features:
/// </para>
/// <list type="bullet">
///   <item><description>User lookup by username (uid) or email</description></item>
///   <item><description>Custom LDAP filter search</description></item>
///   <item><description>Credential validation via bind</description></item>
///   <item><description>Group membership retrieval</description></item>
///   <item><description>SSL/TLS and STARTTLS support</description></item>
/// </list>
/// </remarks>
/// <seealso cref="ILdapService"/>
public sealed class OpenLdapService : BaseAsyncDisposableService, ILdapService
{
    private readonly OpenLdapOptions _options;
    private readonly ILogger<OpenLdapService> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private LdapConnection? _connection;
    private bool _isConnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenLdapService"/> class.
    /// </summary>
    /// <param name="options">The OpenLDAP options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when host or baseDn is empty.</exception>
    public OpenLdapService(
        IOptions<OpenLdapOptions> options,
        ILogger<OpenLdapService> logger)
        : base("OpenLdapService", logger)
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
            "OpenLdapService initialized for {Host}:{Port}",
            _options.Host,
            _options.Port);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenLdapService"/> class.
    /// </summary>
    /// <param name="options">The OpenLDAP options.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenLdapService(
        OpenLdapOptions options,
        ILogger<OpenLdapService> logger)
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

            if (_options.SecurityMode == LdapSecurityMode.Ssl)
            {
                connection.SecureSocketLayer = true;
            }

            await Task.Run(() => connection.Connect(_options.Host, _options.Port), cancellationToken);

            if (_options.SecurityMode == LdapSecurityMode.StartTls)
            {
                await Task.Run(() => connection.StartTls(), cancellationToken);
            }

            await Task.Run(() => connection.Bind(user.DistinguishedName, password), cancellationToken);

            RecordOperation("ValidateCredentials", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "OpenLdap", true);

            _logger.LogDebug("Credentials validated for user: {Username}", username);
            return true;
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            _logger.LogDebug("Invalid credentials for user: {Username}", username);
            RecordOperation("ValidateCredentials", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "OpenLdap", false);
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

    private async Task<PagedResult<LdapUser>> SearchUsersPagedAsync(string filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var skip = (page - 1) * pageSize;

        var searchConstraints = new LdapSearchConstraints
        {
            MaxResults = 0, // No limit for count
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
            var filter = $"(&({_options.GroupObjectClass})(|(cn={escapedName})(displayName={escapedName})))";
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
            DirectoryType = LdapDirectoryType.OpenLdap,
            Id = GetAttributeValue(entry, "entryUUID"),
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
        "entryUUID",
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

    private LdapComputer MapToLdapComputer(LdapEntry entry)
    {
        var ipAddresses = GetMultiValueAttribute(entry, "ipHostNumber");

        var computer = new LdapComputer
        {
            DirectoryType = LdapDirectoryType.OpenLdap,
            Id = GetAttributeValue(entry, "entryUUID"),
            Name = GetAttributeValue(entry, "cn") ?? string.Empty,
            DistinguishedName = entry.Dn,
            DisplayName = GetAttributeValue(entry, "displayName") ?? GetAttributeValue(entry, "cn"),
            Description = GetAttributeValue(entry, "description"),
            Location = GetAttributeValue(entry, "l"),
            IpAddresses = ipAddresses,
            MemberOf = GetMultiValueAttribute(entry, _options.GroupMembershipAttribute)
        };

        return computer;
    }

    private string[] GetComputerAttributes() =>
    [
        "entryUUID",
        "cn",
        "displayName",
        "description",
        "l",
        "ipHostNumber",
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
                LdapAuthenticationMode.SaslExternal => await AuthenticateSaslExternalAsync(options, cancellationToken),
                LdapAuthenticationMode.SaslGssapi => await AuthenticateSaslGssapiAsync(options, cancellationToken),
                LdapAuthenticationMode.Certificate => await AuthenticateSaslExternalAsync(options, cancellationToken),
                _ => throw new NotSupportedException($"Authentication mode {options.Mode} is not supported by OpenLDAP.")
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
    /// Kerberos is supported via SASL GSSAPI. Use <see cref="AuthenticateAsync"/> with
    /// <see cref="LdapAuthenticationMode.SaslGssapi"/> instead.
    /// </exception>
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
                Mode = LdapAuthenticationMode.SaslGssapi,
                Username = username
            };

            var result = await AuthenticateSaslGssapiAsync(options, cancellationToken);

            RecordOperation("AuthenticateWithKerberos", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "SaslGssapi", result.IsAuthenticated);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "AuthenticateWithKerberos", ex.GetType().Name);
            _logger.LogError(ex, "SASL GSSAPI authentication failed for user {Username}", username ?? "(current)");
            throw new InvalidOperationException($"SASL GSSAPI authentication failed: {ex.Message}", ex);
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
                Mode = LdapAuthenticationMode.SaslExternal,
                Certificate = certificate
            };

            var result = await AuthenticateSaslExternalAsync(options, cancellationToken);

            RecordOperation("AuthenticateWithCertificate", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapAuthentication(ServiceName, "SaslExternal", result.IsAuthenticated);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapError(ServiceName, "AuthenticateWithCertificate", ex.GetType().Name);
            _logger.LogError(ex, "SASL EXTERNAL authentication failed");
            throw new InvalidOperationException($"SASL EXTERNAL authentication failed: {ex.Message}", ex);
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
            LdapAuthenticationMode.SaslExternal,
            LdapAuthenticationMode.SaslGssapi,
            LdapAuthenticationMode.Certificate
        ];
    }

    private async Task<LdapAuthenticationResult> AuthenticateSimpleAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        using var connection = new LdapConnection
        {
            ConnectionTimeout = (int)options.Timeout.TotalMilliseconds
        };

        if (_options.SecurityMode == LdapSecurityMode.Ssl)
        {
            connection.SecureSocketLayer = true;
        }

        try
        {
            await Task.Run(() => connection.Connect(_options.Host, _options.Port), cancellationToken);

            if (_options.SecurityMode == LdapSecurityMode.StartTls)
            {
                await Task.Run(() => connection.StartTls(), cancellationToken);
            }

            // Build the bind DN
            var bindDn = BuildBindDn(options.Username!);
            await Task.Run(() => connection.Bind(bindDn, options.Password ?? string.Empty), cancellationToken);

            var result = LdapAuthenticationResult.Success(
                options.Username!,
                LdapAuthenticationMode.Simple,
                LdapDirectoryType.OpenLdap);

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
                LdapDirectoryType.OpenLdap);
        }
        finally
        {
            if (connection.Connected)
            {
                connection.Disconnect();
            }
        }
    }

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
            LdapDirectoryType.OpenLdap);
    }

    private async Task<LdapAuthenticationResult> AuthenticateSaslPlainAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        // SASL PLAIN is similar to simple bind but uses SASL framework
        // The Novell library's simple bind is typically equivalent for most purposes
        using var connection = new LdapConnection
        {
            ConnectionTimeout = (int)options.Timeout.TotalMilliseconds
        };

        if (_options.SecurityMode == LdapSecurityMode.Ssl)
        {
            connection.SecureSocketLayer = true;
        }

        try
        {
            await Task.Run(() => connection.Connect(_options.Host, _options.Port), cancellationToken);

            if (_options.SecurityMode == LdapSecurityMode.StartTls)
            {
                await Task.Run(() => connection.StartTls(), cancellationToken);
            }

            // SASL PLAIN typically uses authcid (authentication identity)
            var bindDn = BuildBindDn(options.Username!);
            await Task.Run(() => connection.Bind(bindDn, options.Password ?? string.Empty), cancellationToken);

            var result = LdapAuthenticationResult.Success(
                options.Username!,
                LdapAuthenticationMode.SaslPlain,
                LdapDirectoryType.OpenLdap);

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
                LdapDirectoryType.OpenLdap);
        }
        finally
        {
            if (connection.Connected)
            {
                connection.Disconnect();
            }
        }
    }

    private Task<LdapAuthenticationResult> AuthenticateSaslExternalAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        // SASL EXTERNAL requires TLS client certificate authentication
        // The Novell library has limited SASL EXTERNAL support
        // This is a simplified implementation

        var certificate = options.GetCertificate();
        if (certificate == null)
        {
            throw new InvalidOperationException("Certificate is required for SASL EXTERNAL authentication.");
        }

        _logger.LogWarning(
            "SASL EXTERNAL authentication requires proper TLS certificate configuration on the LDAP server. " +
            "The Novell.Directory.Ldap library has limited support for this mechanism.");

        // Extract username from certificate subject
        var username = ExtractUsernameFromCertificate(certificate);

        return Task.FromResult(LdapAuthenticationResult.Failure(
            "SASL EXTERNAL is not fully supported by the Novell.Directory.Ldap library. " +
            "Configure TLS client certificate authentication at the server level.",
            "NOT_SUPPORTED",
            LdapAuthenticationMode.SaslExternal,
            LdapDirectoryType.OpenLdap));
    }

    private Task<LdapAuthenticationResult> AuthenticateSaslGssapiAsync(
        LdapAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        // SASL GSSAPI (Kerberos) requires GSSAPI/Kerberos libraries
        // The Novell library has limited SASL GSSAPI support

        _logger.LogWarning(
            "SASL GSSAPI authentication requires Kerberos configuration and is not fully supported " +
            "by the Novell.Directory.Ldap library. Consider using System.DirectoryServices.Protocols " +
            "on Windows or native GSSAPI bindings on Linux.");

        return Task.FromResult(LdapAuthenticationResult.Failure(
            "SASL GSSAPI is not fully supported by the Novell.Directory.Ldap library. " +
            "Use native Kerberos authentication or System.DirectoryServices.Protocols on Windows.",
            "NOT_SUPPORTED",
            LdapAuthenticationMode.SaslGssapi,
            LdapDirectoryType.OpenLdap));
    }

    private string BuildBindDn(string username)
    {
        // If username already looks like a DN, use it as-is
        if (username.Contains('='))
        {
            return username;
        }

        // Build DN using the configured attribute
        return $"{_options.UsernameAttribute}={EscapeLdapDn(username)},{_options.BaseDn}";
    }

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

    private static string ExtractUsernameFromCertificate(X509Certificate2 certificate)
    {
        var subject = certificate.Subject;
        var parts = subject.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(3);
            }
            if (trimmed.StartsWith("UID=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(4);
            }
        }
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
                DirectoryType = LdapDirectoryType.OpenLdap,
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

            if (_options.SecurityMode == LdapSecurityMode.Ssl)
            {
                _connection.SecureSocketLayer = true;
            }

            await Task.Run(() => _connection.Connect(_options.Host, _options.Port), cancellationToken);

            if (_options.SecurityMode == LdapSecurityMode.StartTls)
            {
                await Task.Run(() => _connection.StartTls(), cancellationToken);
            }

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
            ToolboxMeter.RecordLdapConnection(ServiceName, "OpenLdap", _options.Host, true);
            _logger.LogDebug("Connected to OpenLDAP: {Host}:{Port}", _options.Host, _options.Port);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ToolboxMeter.RecordLdapConnection(ServiceName, "OpenLdap", _options.Host, false);
            ToolboxMeter.RecordLdapError(ServiceName, "EnsureConnected", ex.GetType().Name);
            _logger.LogError(ex, "Failed to connect to OpenLDAP");
            throw new InvalidOperationException($"Failed to connect to OpenLDAP: {ex.Message}", ex);
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
            DirectoryType = LdapDirectoryType.OpenLdap,
            Id = GetAttributeValue(entry, "entryUUID"),
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
            "entryUUID",
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
