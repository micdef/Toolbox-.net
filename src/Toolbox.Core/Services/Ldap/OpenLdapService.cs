// @file OpenLdapService.cs
// @brief OpenLDAP service implementation
// @details Implements ILdapService for OpenLDAP and compatible directories
// @note Uses Novell.Directory.Ldap.NETStandard for cross-platform support

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
            _logger.LogDebug("Connected to OpenLDAP: {Host}:{Port}", _options.Host, _options.Port);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
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
