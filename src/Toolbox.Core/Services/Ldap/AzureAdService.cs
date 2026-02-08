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
                ToolboxMeter.RecordLdapQuery(ServiceName, "GetUserByUsername", false);
                return null;
            }

            var user = MapToLdapUser(graphUser);
            RecordOperation("GetUserByUsername", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQuery(ServiceName, "GetUserByUsername", true);

            return user;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
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
                ToolboxMeter.RecordLdapQuery(ServiceName, "GetUserByEmail", false);
                return null;
            }

            var user = MapToLdapUser(graphUser);
            RecordOperation("GetUserByEmail", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordLdapQuery(ServiceName, "GetUserByEmail", true);

            return user;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
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
            ToolboxMeter.RecordLdapQuery(ServiceName, "SearchUsers", true);

            return result;
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
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
            _logger.LogError(ex, "Graph API error getting groups for user: {Username}", username);
            throw new InvalidOperationException($"Failed to get user groups: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        // GraphServiceClient doesn't need explicit disposal
        return ValueTask.CompletedTask;
    }

    private GraphServiceClient CreateGraphClient()
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

        return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
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
