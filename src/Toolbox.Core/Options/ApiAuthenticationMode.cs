// @file ApiAuthenticationMode.cs
// @brief API authentication modes enumeration
// @details Defines the supported authentication methods for API requests
// @note Each mode requires specific configuration options

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the authentication mode for API requests.
/// </summary>
/// <remarks>
/// <para>
/// Choose the appropriate authentication mode based on the API requirements.
/// Each mode requires specific configuration in <see cref="ApiOptions"/>.
/// </para>
/// </remarks>
public enum ApiAuthenticationMode
{
    /// <summary>
    /// No authentication required.
    /// </summary>
    Anonymous = 0,

    /// <summary>
    /// Bearer token authentication.
    /// Uses the Authorization header with "Bearer {token}" format.
    /// </summary>
    BearerToken = 1,

    /// <summary>
    /// Basic authentication with username and password.
    /// Uses the Authorization header with Base64-encoded credentials.
    /// </summary>
    Basic = 2,

    /// <summary>
    /// API key authentication.
    /// Can be sent as a header or query parameter.
    /// </summary>
    ApiKey = 3,

    /// <summary>
    /// Client certificate authentication.
    /// Uses X.509 certificate for mutual TLS (mTLS).
    /// </summary>
    Certificate = 4,

    /// <summary>
    /// OAuth2 client credentials flow.
    /// Automatically obtains and refreshes access tokens.
    /// </summary>
    OAuth2ClientCredentials = 5
}

/// <summary>
/// Specifies where to send the API key.
/// </summary>
public enum ApiKeyLocation
{
    /// <summary>
    /// Send API key in a header.
    /// </summary>
    Header = 0,

    /// <summary>
    /// Send API key as a query parameter.
    /// </summary>
    QueryString = 1
}
