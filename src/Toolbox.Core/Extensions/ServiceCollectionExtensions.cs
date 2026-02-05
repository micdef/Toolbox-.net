// @file ServiceCollectionExtensions.cs
// @brief Extension methods for IServiceCollection to register Toolbox services
// @details Provides fluent API for configuring Toolbox in dependency injection
// @note Use AddToolboxCore() to register base Toolbox services

using Microsoft.Extensions.Configuration;
using Toolbox.Core.Options;

namespace Toolbox.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to configure Toolbox services.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide a fluent API for registering Toolbox services
/// with the dependency injection container.
/// </para>
/// <example>
/// <code>
/// services.AddToolboxCore(options =>
/// {
///     options.EnableDetailedTelemetry = true;
///     options.ServicePrefix = "MyApp";
/// });
/// </code>
/// </example>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Toolbox core services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method registers the default <see cref="ToolboxOptions"/> with no custom configuration.
    /// </remarks>
    public static IServiceCollection AddToolboxCore(this IServiceCollection services)
    {
        return services.AddToolboxCore(_ => { });
    }

    /// <summary>
    /// Adds Toolbox core services to the specified <see cref="IServiceCollection"/>
    /// with custom options configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">An action to configure the <see cref="ToolboxOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is <c>null</c>.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddToolboxCore(options =>
    /// {
    ///     options.EnableDetailedTelemetry = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddToolboxCore(
        this IServiceCollection services,
        Action<ToolboxOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);

        return services;
    }

    /// <summary>
    /// Adds Toolbox core services to the specified <see cref="IServiceCollection"/>
    /// with configuration binding from <see cref="IConfiguration"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration to bind options from.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// This method binds options from the <c>Toolbox</c> configuration section.
    /// </remarks>
    /// <example>
    /// <code>
    /// // In appsettings.json:
    /// // { "Toolbox": { "EnableDetailedTelemetry": true } }
    ///
    /// services.AddToolboxCore(configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddToolboxCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ToolboxOptions>(
            configuration.GetSection(ToolboxOptions.SectionName));

        return services;
    }
}
