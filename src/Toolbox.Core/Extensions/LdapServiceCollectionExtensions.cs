// @file LdapServiceCollectionExtensions.cs
// @brief DI extensions for LDAP services
// @details Provides extension methods to register LDAP services
// @note Includes configuration via IOptions pattern

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Ldap;

namespace Toolbox.Core.Extensions;

/// <summary>
/// Extension methods for registering LDAP services with dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// These extensions simplify the registration of LDAP services
/// with the Microsoft.Extensions.DependencyInjection container.
/// </para>
/// <para>
/// Available services:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="AddActiveDirectory(IServiceCollection, Action{ActiveDirectoryOptions})"/> - Windows AD</description></item>
///   <item><description><see cref="AddAzureAd(IServiceCollection, Action{AzureAdOptions})"/> - Azure AD / Entra ID</description></item>
///   <item><description><see cref="AddOpenLdap(IServiceCollection, Action{OpenLdapOptions})"/> - OpenLDAP / Linux</description></item>
///   <item><description><see cref="AddAppleDirectory(IServiceCollection, Action{AppleDirectoryOptions})"/> - macOS Open Directory</description></item>
/// </list>
/// </remarks>
public static class LdapServiceCollectionExtensions
{
    #region Active Directory

    /// <summary>
    /// Adds the Active Directory LDAP service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure Active Directory options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// services.AddActiveDirectory(options =>
    /// {
    ///     options.Domain = "corp.example.com";
    ///     options.UseCurrentCredentials = true;
    ///     options.UseSsl = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddActiveDirectory(
        this IServiceCollection services,
        Action<ActiveDirectoryOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.TryAddSingleton<ILdapService, ActiveDirectoryService>();

        return services;
    }

    /// <summary>
    /// Adds the Active Directory LDAP service with configuration binding.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section containing AD options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// // In appsettings.json:
    /// // {
    /// //   "Toolbox": {
    /// //     "Ldap": {
    /// //       "ActiveDirectory": {
    /// //         "Domain": "corp.example.com",
    /// //         "UseSsl": true
    /// //       }
    /// //     }
    /// //   }
    /// // }
    ///
    /// services.AddActiveDirectory(configuration.GetSection("Toolbox:Ldap:ActiveDirectory"));
    /// </code>
    /// </example>
    public static IServiceCollection AddActiveDirectory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ActiveDirectoryOptions>(configuration);
        services.TryAddSingleton<ILdapService, ActiveDirectoryService>();

        return services;
    }

    /// <summary>
    /// Adds the Active Directory LDAP service with pre-configured options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The Active Directory options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddActiveDirectory(
        this IServiceCollection services,
        ActiveDirectoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.TryAddSingleton<ILdapService, ActiveDirectoryService>();

        return services;
    }

    #endregion

    #region Azure AD

    /// <summary>
    /// Adds the Azure AD service using Microsoft Graph API.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure Azure AD options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// services.AddAzureAd(options =>
    /// {
    ///     options.TenantId = "your-tenant-id";
    ///     options.ClientId = "your-client-id";
    ///     options.ClientSecret = "your-client-secret";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureAd(
        this IServiceCollection services,
        Action<AzureAdOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.TryAddSingleton<ILdapService, AzureAdService>();

        return services;
    }

    /// <summary>
    /// Adds the Azure AD service with configuration binding.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section containing Azure AD options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddAzureAd(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AzureAdOptions>(configuration);
        services.TryAddSingleton<ILdapService, AzureAdService>();

        return services;
    }

    /// <summary>
    /// Adds the Azure AD service with pre-configured options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The Azure AD options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddAzureAd(
        this IServiceCollection services,
        AzureAdOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.TryAddSingleton<ILdapService, AzureAdService>();

        return services;
    }

    /// <summary>
    /// Adds the Azure AD service with managed identity authentication.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="tenantId">The Azure AD tenant ID.</param>
    /// <param name="clientId">The application client ID.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <remarks>
    /// Use this method for applications hosted in Azure that use Managed Identity.
    /// </remarks>
    public static IServiceCollection AddAzureAdWithManagedIdentity(
        this IServiceCollection services,
        string tenantId,
        string clientId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(clientId);

        return services.AddAzureAd(options =>
        {
            options.TenantId = tenantId;
            options.ClientId = clientId;
            options.UseManagedIdentity = true;
            options.AuthenticationMode = AzureAdAuthMode.ManagedIdentity;
        });
    }

    #endregion

    #region OpenLDAP

    /// <summary>
    /// Adds the OpenLDAP service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure OpenLDAP options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// services.AddOpenLdap(options =>
    /// {
    ///     options.Host = "ldap.example.com";
    ///     options.Port = 389;
    ///     options.BaseDn = "dc=example,dc=com";
    ///     options.BindDn = "cn=admin,dc=example,dc=com";
    ///     options.BindPassword = "secret";
    ///     options.SecurityMode = LdapSecurityMode.StartTls;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOpenLdap(
        this IServiceCollection services,
        Action<OpenLdapOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.TryAddSingleton<ILdapService, OpenLdapService>();

        return services;
    }

    /// <summary>
    /// Adds the OpenLDAP service with configuration binding.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section containing OpenLDAP options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddOpenLdap(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<OpenLdapOptions>(configuration);
        services.TryAddSingleton<ILdapService, OpenLdapService>();

        return services;
    }

    /// <summary>
    /// Adds the OpenLDAP service with pre-configured options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The OpenLDAP options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddOpenLdap(
        this IServiceCollection services,
        OpenLdapOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.TryAddSingleton<ILdapService, OpenLdapService>();

        return services;
    }

    #endregion

    #region Apple Directory

    /// <summary>
    /// Adds the Apple Directory service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure Apple Directory options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// services.AddAppleDirectory(options =>
    /// {
    ///     options.Host = "od.example.com";
    ///     options.Port = 389;
    ///     options.BaseDn = "dc=example,dc=com";
    ///     options.BindDn = "uid=admin,cn=users,dc=example,dc=com";
    ///     options.BindPassword = "secret";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAppleDirectory(
        this IServiceCollection services,
        Action<AppleDirectoryOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.TryAddSingleton<ILdapService, AppleDirectoryService>();

        return services;
    }

    /// <summary>
    /// Adds the Apple Directory service with configuration binding.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section containing Apple Directory options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddAppleDirectory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AppleDirectoryOptions>(configuration);
        services.TryAddSingleton<ILdapService, AppleDirectoryService>();

        return services;
    }

    /// <summary>
    /// Adds the Apple Directory service with pre-configured options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The Apple Directory options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddAppleDirectory(
        this IServiceCollection services,
        AppleDirectoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.TryAddSingleton<ILdapService, AppleDirectoryService>();

        return services;
    }

    #endregion
}
