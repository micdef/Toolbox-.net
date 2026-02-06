// @file ApiOptions.cs
// @brief Configuration options for the API service
// @details Contains HTTP client and authentication settings
// @note Supports multiple authentication modes

using System.Security.Cryptography.X509Certificates;

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for the HTTP API service.
/// </summary>
/// <remarks>
/// <para>
/// These options configure HTTP client behavior and authentication.
/// The authentication mode determines which credential properties are used.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var options = new ApiOptions
/// {
///     BaseUrl = "https://api.example.com",
///     AuthenticationMode = ApiAuthenticationMode.BearerToken,
///     BearerToken = "your-token-here",
///     Timeout = TimeSpan.FromSeconds(30)
/// };
/// </code>
/// </example>
public sealed class ApiOptions
{
    /// <summary>
    /// Gets or sets the base URL for API requests.
    /// </summary>
    /// <value>The base URL (e.g., "https://api.example.com").</value>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the authentication mode.
    /// </summary>
    /// <value>The authentication mode. Default is <see cref="ApiAuthenticationMode.Anonymous"/>.</value>
    public ApiAuthenticationMode AuthenticationMode { get; set; } = ApiAuthenticationMode.Anonymous;

    #region Bearer Token Authentication

    /// <summary>
    /// Gets or sets the bearer token for authentication.
    /// </summary>
    /// <value>The bearer token (without "Bearer" prefix).</value>
    /// <remarks>Used when <see cref="AuthenticationMode"/> is <see cref="ApiAuthenticationMode.BearerToken"/>.</remarks>
    public string? BearerToken { get; set; }

    #endregion

    #region Basic Authentication

    /// <summary>
    /// Gets or sets the username for basic authentication.
    /// </summary>
    /// <value>The username.</value>
    /// <remarks>Used when <see cref="AuthenticationMode"/> is <see cref="ApiAuthenticationMode.Basic"/>.</remarks>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for basic authentication.
    /// </summary>
    /// <value>The password.</value>
    /// <remarks>Used when <see cref="AuthenticationMode"/> is <see cref="ApiAuthenticationMode.Basic"/>.</remarks>
    public string? Password { get; set; }

    #endregion

    #region API Key Authentication

    /// <summary>
    /// Gets or sets the API key value.
    /// </summary>
    /// <value>The API key.</value>
    /// <remarks>Used when <see cref="AuthenticationMode"/> is <see cref="ApiAuthenticationMode.ApiKey"/>.</remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API key header or parameter name.
    /// </summary>
    /// <value>The name (e.g., "X-API-Key" or "api_key"). Default is "X-API-Key".</value>
    public string ApiKeyName { get; set; } = "X-API-Key";

    /// <summary>
    /// Gets or sets where to send the API key.
    /// </summary>
    /// <value>The location. Default is <see cref="ApiKeyLocation.Header"/>.</value>
    public ApiKeyLocation ApiKeyLocation { get; set; } = ApiKeyLocation.Header;

    #endregion

    #region Certificate Authentication

    /// <summary>
    /// Gets or sets the client certificate for authentication.
    /// </summary>
    /// <value>The X.509 certificate.</value>
    /// <remarks>Used when <see cref="AuthenticationMode"/> is <see cref="ApiAuthenticationMode.Certificate"/>.</remarks>
    public X509Certificate2? ClientCertificate { get; set; }

    /// <summary>
    /// Gets or sets the path to a PFX/PKCS#12 certificate file.
    /// </summary>
    /// <value>The file path.</value>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the certificate password.
    /// </summary>
    /// <value>The password for the certificate file.</value>
    public string? CertificatePassword { get; set; }

    #endregion

    #region OAuth2 Client Credentials

    /// <summary>
    /// Gets or sets the OAuth2 token endpoint URL.
    /// </summary>
    /// <value>The token endpoint (e.g., "https://auth.example.com/oauth/token").</value>
    public string? OAuth2TokenUrl { get; set; }

    /// <summary>
    /// Gets or sets the OAuth2 client ID.
    /// </summary>
    /// <value>The client ID.</value>
    public string? OAuth2ClientId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth2 client secret.
    /// </summary>
    /// <value>The client secret.</value>
    public string? OAuth2ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the OAuth2 scopes.
    /// </summary>
    /// <value>Space-separated scopes (e.g., "read write").</value>
    public string? OAuth2Scopes { get; set; }

    #endregion

    #region HTTP Client Settings

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    /// <value>The timeout. Default is 30 seconds.</value>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    /// <value>The retry count. Default is 3.</value>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retries.
    /// </summary>
    /// <value>The retry delay. Default is 1 second.</value>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether to use exponential backoff for retries.
    /// </summary>
    /// <value><c>true</c> for exponential backoff; <c>false</c> for fixed delay. Default is <c>true</c>.</value>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate SSL certificates.
    /// </summary>
    /// <value><c>true</c> to validate (recommended); <c>false</c> to skip. Default is <c>true</c>.</value>
    public bool ValidateCertificate { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to follow redirects automatically.
    /// </summary>
    /// <value><c>true</c> to follow redirects; <c>false</c> to return redirect responses. Default is <c>true</c>.</value>
    public bool FollowRedirects { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of redirects to follow.
    /// </summary>
    /// <value>The maximum redirects. Default is 10.</value>
    public int MaxRedirects { get; set; } = 10;

    /// <summary>
    /// Gets or sets the default User-Agent header.
    /// </summary>
    /// <value>The User-Agent string.</value>
    public string? UserAgent { get; set; } = "Toolbox.Core.HttpApiService/1.0";

    /// <summary>
    /// Gets the default headers to include in all requests.
    /// </summary>
    /// <value>Dictionary of header names and values.</value>
    public IDictionary<string, string> DefaultHeaders { get; } = new Dictionary<string, string>();

    #endregion
}
