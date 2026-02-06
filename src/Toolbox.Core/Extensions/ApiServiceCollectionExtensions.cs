// @file ApiServiceCollectionExtensions.cs
// @brief DI extensions for API services
// @details Provides extension methods to register API services
// @note Includes configuration via IOptions pattern

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Api;

namespace Toolbox.Core.Extensions;

/// <summary>
/// Extension methods for registering API services with dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// These extensions simplify the registration of HTTP API services
/// with the Microsoft.Extensions.DependencyInjection container.
/// </para>
/// </remarks>
public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds the HTTP API service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure API options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// services.AddHttpApi(options =>
    /// {
    ///     options.BaseUrl = "https://api.example.com";
    ///     options.AuthenticationMode = ApiAuthenticationMode.BearerToken;
    ///     options.BearerToken = "your-token";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddHttpApi(
        this IServiceCollection services,
        Action<ApiOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.TryAddSingleton<IApiService, HttpApiService>();

        return services;
    }

    /// <summary>
    /// Adds the HTTP API service to the service collection with a configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section containing API options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// // In appsettings.json:
    /// // {
    /// //   "Api": {
    /// //     "BaseUrl": "https://api.example.com",
    /// //     "AuthenticationMode": "BearerToken",
    /// //     "BearerToken": "your-token"
    /// //   }
    /// // }
    ///
    /// services.AddHttpApi(configuration.GetSection("Api"));
    /// </code>
    /// </example>
    public static IServiceCollection AddHttpApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ApiOptions>(configuration);
        services.TryAddSingleton<IApiService, HttpApiService>();

        return services;
    }

    /// <summary>
    /// Adds the HTTP API service with pre-configured options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The API options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddHttpApi(
        this IServiceCollection services,
        ApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.TryAddSingleton<IApiService, HttpApiService>();

        return services;
    }

    /// <summary>
    /// Adds an anonymous HTTP API service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseUrl">The base URL for the API.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <c>null</c>.</exception>
    public static IServiceCollection AddHttpApiAnonymous(
        this IServiceCollection services,
        string? baseUrl = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddHttpApi(options =>
        {
            options.BaseUrl = baseUrl;
            options.AuthenticationMode = ApiAuthenticationMode.Anonymous;
        });
    }

    /// <summary>
    /// Adds an HTTP API service with Bearer token authentication.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseUrl">The base URL for the API.</param>
    /// <param name="bearerToken">The bearer token.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddHttpApiWithBearerToken(
        this IServiceCollection services,
        string baseUrl,
        string bearerToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(bearerToken);

        return services.AddHttpApi(options =>
        {
            options.BaseUrl = baseUrl;
            options.AuthenticationMode = ApiAuthenticationMode.BearerToken;
            options.BearerToken = bearerToken;
        });
    }

    /// <summary>
    /// Adds an HTTP API service with Basic authentication.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseUrl">The base URL for the API.</param>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddHttpApiWithBasicAuth(
        this IServiceCollection services,
        string baseUrl,
        string username,
        string password)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        return services.AddHttpApi(options =>
        {
            options.BaseUrl = baseUrl;
            options.AuthenticationMode = ApiAuthenticationMode.Basic;
            options.Username = username;
            options.Password = password;
        });
    }

    /// <summary>
    /// Adds an HTTP API service with API key authentication.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseUrl">The base URL for the API.</param>
    /// <param name="apiKey">The API key.</param>
    /// <param name="apiKeyName">The header or query parameter name. Default is "X-API-Key".</param>
    /// <param name="location">Where to send the API key. Default is <see cref="ApiKeyLocation.Header"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddHttpApiWithApiKey(
        this IServiceCollection services,
        string baseUrl,
        string apiKey,
        string apiKeyName = "X-API-Key",
        ApiKeyLocation location = ApiKeyLocation.Header)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(apiKey);

        return services.AddHttpApi(options =>
        {
            options.BaseUrl = baseUrl;
            options.AuthenticationMode = ApiAuthenticationMode.ApiKey;
            options.ApiKey = apiKey;
            options.ApiKeyName = apiKeyName;
            options.ApiKeyLocation = location;
        });
    }

    /// <summary>
    /// Adds an HTTP API service with client certificate authentication.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseUrl">The base URL for the API.</param>
    /// <param name="certificate">The client certificate.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddHttpApiWithCertificate(
        this IServiceCollection services,
        string baseUrl,
        X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(certificate);

        return services.AddHttpApi(options =>
        {
            options.BaseUrl = baseUrl;
            options.AuthenticationMode = ApiAuthenticationMode.Certificate;
            options.ClientCertificate = certificate;
        });
    }

    /// <summary>
    /// Adds an HTTP API service with client certificate authentication from file.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseUrl">The base URL for the API.</param>
    /// <param name="certificatePath">Path to the PFX certificate file.</param>
    /// <param name="certificatePassword">The certificate password.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddHttpApiWithCertificate(
        this IServiceCollection services,
        string baseUrl,
        string certificatePath,
        string? certificatePassword = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(certificatePath);

        return services.AddHttpApi(options =>
        {
            options.BaseUrl = baseUrl;
            options.AuthenticationMode = ApiAuthenticationMode.Certificate;
            options.CertificatePath = certificatePath;
            options.CertificatePassword = certificatePassword;
        });
    }

    /// <summary>
    /// Adds an HTTP API service with OAuth2 client credentials flow.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseUrl">The base URL for the API.</param>
    /// <param name="tokenUrl">The OAuth2 token endpoint URL.</param>
    /// <param name="clientId">The OAuth2 client ID.</param>
    /// <param name="clientSecret">The OAuth2 client secret.</param>
    /// <param name="scopes">Optional scopes (space-separated).</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddHttpApiWithOAuth2(
        this IServiceCollection services,
        string baseUrl,
        string tokenUrl,
        string clientId,
        string clientSecret,
        string? scopes = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(tokenUrl);
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(clientSecret);

        return services.AddHttpApi(options =>
        {
            options.BaseUrl = baseUrl;
            options.AuthenticationMode = ApiAuthenticationMode.OAuth2ClientCredentials;
            options.OAuth2TokenUrl = tokenUrl;
            options.OAuth2ClientId = clientId;
            options.OAuth2ClientSecret = clientSecret;
            options.OAuth2Scopes = scopes;
        });
    }
}
