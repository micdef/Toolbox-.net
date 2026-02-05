// @file CryptographyServiceCollectionExtensions.cs
// @brief Extension methods for registering cryptography services
// @details Provides fluent API for adding cryptography services to DI
// @note Use AddBase64Cryptography() or AddAesCryptography() to register services

using Microsoft.Extensions.Configuration;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Cryptography;

namespace Toolbox.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to configure cryptography services.
/// </summary>
/// <remarks>
/// These extensions provide a fluent API for registering cryptography services.
/// </remarks>
public static class CryptographyServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Base64 cryptography service with the specified encoding table.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="encodingTable">The encoding table to use.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// services.AddBase64Cryptography(Base64EncodingTable.UrlSafe);
    /// </code>
    /// </example>
    public static IServiceCollection AddBase64Cryptography(
        this IServiceCollection services,
        Base64EncodingTable encodingTable)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<Base64CryptographyOptions>(options =>
        {
            options.EncodingTable = encodingTable;
        });

        services.AddScoped<ICryptographyService, Base64CryptographyService>();

        return services;
    }

    /// <summary>
    /// Adds the Base64 cryptography service with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An action to configure the options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is <c>null</c>.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddBase64Cryptography(options =>
    /// {
    ///     options.EncodingTable = Base64EncodingTable.UrlSafe;
    ///     options.IncludePadding = false;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddBase64Cryptography(
        this IServiceCollection services,
        Action<Base64CryptographyOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.AddScoped<ICryptographyService, Base64CryptographyService>();

        return services;
    }

    /// <summary>
    /// Adds the Base64 cryptography service with configuration binding.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Binds options from the <c>Toolbox:Cryptography:Base64</c> configuration section.
    /// </remarks>
    public static IServiceCollection AddBase64Cryptography(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<Base64CryptographyOptions>(
            configuration.GetSection(Base64CryptographyOptions.SectionName));

        services.AddScoped<ICryptographyService, Base64CryptographyService>();

        return services;
    }

    /// <summary>
    /// Adds the AES cryptography service with the specified key and IV.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The AES encryption key (16, 24, or 32 bytes).</param>
    /// <param name="iv">The initialization vector (16 bytes).</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/>, <paramref name="key"/>, or <paramref name="iv"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when key or IV size is invalid.</exception>
    /// <example>
    /// <code>
    /// var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
    /// services.AddAesCryptography(key, iv);
    /// </code>
    /// </example>
    public static IServiceCollection AddAesCryptography(
        this IServiceCollection services,
        byte[] key,
        byte[] iv)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(iv);

        services.AddScoped<ICryptographyService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AesCryptographyService>>();
            return new AesCryptographyService(key, iv, logger);
        });

        return services;
    }
}
