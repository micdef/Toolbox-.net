// @file SsoServiceCollectionExtensions.cs
// @brief Extension methods for registering SSO services in dependency injection
// @details Provides fluent configuration for SSO session management and credential storage

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Sso;

namespace Toolbox.Core.Extensions;

/// <summary>
/// Extension methods for adding SSO services to the dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide a fluent API for configuring SSO session management,
/// credential storage, and automatic token refresh.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddSsoServices(sso =>
/// {
///     sso.EnableAutoRefresh = true;
///     sso.RefreshThreshold = 0.8;
///     sso.MaxSessionsPerUser = 5;
/// });
/// </code>
/// </example>
public static class SsoServiceCollectionExtensions
{
    #region Configuration Section Names

    /// <summary>
    /// The configuration section name for SSO session options.
    /// </summary>
    public const string SsoSessionOptionsSection = "Toolbox:Sso:Session";

    /// <summary>
    /// The configuration section name for credential store options.
    /// </summary>
    public const string CredentialStoreOptionsSection = "Toolbox:Sso:CredentialStore";

    #endregion

    #region AddSsoServices Overloads

    /// <summary>
    /// Adds SSO services to the service collection with default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Registers:
    /// <list type="bullet">
    ///   <item><description><see cref="ISsoSessionManager"/> as singleton</description></item>
    ///   <item><description><see cref="ICredentialStore"/> as singleton (auto-detected platform)</description></item>
    ///   <item><description><see cref="ITokenRefreshService"/> as singleton and hosted service</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddSsoServices(this IServiceCollection services)
    {
        return services.AddSsoServices(
            configureSso: null,
            configureCredStore: null);
    }

    /// <summary>
    /// Adds SSO services to the service collection with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureSso">Optional action to configure SSO session options.</param>
    /// <param name="configureCredStore">Optional action to configure credential store options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSsoServices(
        this IServiceCollection services,
        Action<SsoSessionOptions>? configureSso = null,
        Action<CredentialStoreOptions>? configureCredStore = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Configure options
        if (configureSso != null)
        {
            services.Configure(configureSso);
        }
        else
        {
            services.AddOptions<SsoSessionOptions>();
        }

        if (configureCredStore != null)
        {
            services.Configure(configureCredStore);
        }
        else
        {
            services.AddOptions<CredentialStoreOptions>();
        }

        // Register credential store (platform-specific)
        services.TryAddSingleton<ICredentialStore>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CredentialStoreOptions>>();
            return CreateCredentialStore(sp, options.Value);
        });

        // Register token refresh service
        services.TryAddSingleton<ITokenRefreshService, TokenRefreshService>();

        // Register as hosted service if auto-refresh is enabled
        services.AddSingleton<IHostedService>(sp =>
        {
            var refreshService = sp.GetRequiredService<ITokenRefreshService>();
            return (IHostedService)refreshService;
        });

        // Register session manager
        services.TryAddSingleton<ISsoSessionManager, SsoSessionManager>();

        return services;
    }

    /// <summary>
    /// Adds SSO services to the service collection from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Reads configuration from:
    /// <list type="bullet">
    ///   <item><description><c>Toolbox:Sso:Session</c> for SSO session options</description></item>
    ///   <item><description><c>Toolbox:Sso:CredentialStore</c> for credential store options</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddSsoServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind options from configuration
        services.Configure<SsoSessionOptions>(
            configuration.GetSection(SsoSessionOptionsSection));

        services.Configure<CredentialStoreOptions>(
            configuration.GetSection(CredentialStoreOptionsSection));

        // Register services
        return services.AddSsoServices(
            configureSso: null,
            configureCredStore: null);
    }

    #endregion

    #region Credential Store Registration

    /// <summary>
    /// Adds a specific credential store implementation.
    /// </summary>
    /// <typeparam name="TStore">The credential store implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCredentialStore<TStore>(
        this IServiceCollection services,
        Action<CredentialStoreOptions>? configureOptions = null)
        where TStore : class, ICredentialStore
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.AddOptions<CredentialStoreOptions>();
        }

        services.AddSingleton<ICredentialStore, TStore>();
        return services;
    }

    /// <summary>
    /// Adds the Windows Credential Manager credential store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when called on a non-Windows platform.
    /// </exception>
    public static IServiceCollection AddWindowsCredentialStore(
        this IServiceCollection services,
        Action<CredentialStoreOptions>? configureOptions = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows Credential Manager is only available on Windows");
        }

        return services.AddCredentialStore<WindowsCredentialStore>(configureOptions);
    }

    /// <summary>
    /// Adds the fallback encrypted file credential store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFallbackCredentialStore(
        this IServiceCollection services,
        Action<CredentialStoreOptions>? configureOptions = null)
    {
        return services.AddCredentialStore<FallbackCredentialStore>(configureOptions);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Creates the appropriate credential store based on platform and options.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="options">The credential store options.</param>
    /// <returns>The credential store instance.</returns>
    private static ICredentialStore CreateCredentialStore(
        IServiceProvider serviceProvider,
        CredentialStoreOptions options)
    {
        var provider = options.Provider;

        // Auto-detect platform if set to Auto
        if (provider == CredentialStoreProvider.Auto)
        {
            provider = DetectBestProvider(options);
        }

        return provider switch
        {
            CredentialStoreProvider.WindowsCredentialManager when OperatingSystem.IsWindows()
                => ActivatorUtilities.CreateInstance<WindowsCredentialStore>(serviceProvider),

            CredentialStoreProvider.EncryptedFile
                => ActivatorUtilities.CreateInstance<FallbackCredentialStore>(serviceProvider),

            CredentialStoreProvider.InMemory
                => ActivatorUtilities.CreateInstance<InMemoryCredentialStore>(serviceProvider),

            // Fallback to encrypted file for unsupported providers
            _ => ActivatorUtilities.CreateInstance<FallbackCredentialStore>(serviceProvider)
        };
    }

    /// <summary>
    /// Detects the best credential store provider for the current platform.
    /// </summary>
    /// <param name="options">The credential store options.</param>
    /// <returns>The detected provider.</returns>
    private static CredentialStoreProvider DetectBestProvider(CredentialStoreOptions options)
    {
        if (!options.UseOsKeychain)
        {
            return CredentialStoreProvider.EncryptedFile;
        }

        if (OperatingSystem.IsWindows())
        {
            return CredentialStoreProvider.WindowsCredentialManager;
        }

        if (OperatingSystem.IsMacOS())
        {
            // macOS Keychain would be implemented separately
            return CredentialStoreProvider.EncryptedFile;
        }

        if (OperatingSystem.IsLinux())
        {
            // Linux Secret Service would be implemented separately
            return CredentialStoreProvider.EncryptedFile;
        }

        return CredentialStoreProvider.EncryptedFile;
    }

    #endregion
}
