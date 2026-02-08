// @file AzureAdOptions.cs
// @brief Configuration options for Azure Active Directory
// @details Settings for connecting to Azure AD via Microsoft Graph API
// @note Azure AD does not support direct LDAP; uses Graph API instead

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for Azure Active Directory / Microsoft Entra ID.
/// </summary>
/// <remarks>
/// <para>
/// Azure AD does not support direct LDAP connections (except via Azure AD DS).
/// This service uses Microsoft Graph API for user queries, which provides
/// full access to Azure AD directory data.
/// </para>
/// <para>
/// Supported authentication modes:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="AzureAdAuthMode.ClientSecret"/>: Application secret</description></item>
///   <item><description><see cref="AzureAdAuthMode.Certificate"/>: X.509 certificate</description></item>
///   <item><description><see cref="AzureAdAuthMode.ManagedIdentity"/>: Azure managed identity</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var options = new AzureAdOptions
/// {
///     TenantId = "your-tenant-id",
///     ClientId = "your-client-id",
///     ClientSecret = "your-client-secret",
///     AuthenticationMode = AzureAdAuthMode.ClientSecret
/// };
/// </code>
/// </example>
public sealed class AzureAdOptions
{
    /// <summary>
    /// The configuration section name for binding from appsettings.json.
    /// </summary>
    public const string SectionName = "Toolbox:Ldap:AzureAd";

    /// <summary>
    /// Gets or sets the Azure AD tenant ID.
    /// </summary>
    /// <value>The tenant ID (GUID) or domain name.</value>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure AD application (client) ID.
    /// </summary>
    /// <value>The application registration client ID.</value>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret for application authentication.
    /// </summary>
    /// <value>Required when using <see cref="AzureAdAuthMode.ClientSecret"/>.</value>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the certificate thumbprint for certificate-based auth.
    /// </summary>
    /// <value>The SHA-1 thumbprint of the certificate in the local store.</value>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Gets or sets the path to the certificate file.
    /// </summary>
    /// <value>Path to .pfx or .pem certificate file.</value>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the certificate password.
    /// </summary>
    /// <value>Password for the certificate file.</value>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Gets or sets the authentication mode.
    /// </summary>
    /// <value>Default is <see cref="AzureAdAuthMode.ClientSecret"/>.</value>
    public AzureAdAuthMode AuthenticationMode { get; set; } = AzureAdAuthMode.ClientSecret;

    /// <summary>
    /// Gets or sets whether to use managed identity (for Azure-hosted apps).
    /// </summary>
    /// <value>
    /// When <c>true</c>, uses Azure Managed Identity and ignores other credentials.
    /// Only works for apps hosted in Azure.
    /// </value>
    public bool UseManagedIdentity { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft Graph API base URL.
    /// </summary>
    /// <value>Default is the v1.0 endpoint.</value>
    public string GraphApiBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";

    /// <summary>
    /// Gets or sets additional properties to select in user queries.
    /// </summary>
    /// <value>List of Graph API user properties to retrieve.</value>
    public IList<string> SelectProperties { get; set; } =
    [
        "id",
        "displayName",
        "givenName",
        "surname",
        "mail",
        "userPrincipalName",
        "mailNickname",
        "jobTitle",
        "department",
        "officeLocation",
        "mobilePhone",
        "businessPhones",
        "streetAddress",
        "city",
        "state",
        "postalCode",
        "country",
        "accountEnabled",
        "createdDateTime"
    ];

    /// <summary>
    /// Gets or sets the connection timeout.
    /// </summary>
    /// <value>Default is 30 seconds.</value>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the operation timeout.
    /// </summary>
    /// <value>Default is 60 seconds.</value>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
