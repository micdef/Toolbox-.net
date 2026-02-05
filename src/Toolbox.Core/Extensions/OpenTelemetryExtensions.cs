// @file OpenTelemetryExtensions.cs
// @brief Extension methods for configuring OpenTelemetry with Toolbox
// @details Provides fluent API for adding Toolbox tracing and metrics to OpenTelemetry
// @note Use AddToolboxInstrumentation() on TracerProviderBuilder and MeterProviderBuilder

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Resources;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Extensions;

/// <summary>
/// Extension methods for configuring OpenTelemetry with Toolbox instrumentation.
/// </summary>
/// <remarks>
/// <para>
/// These extensions simplify the integration of Toolbox telemetry with OpenTelemetry.
/// They register the Toolbox activity source and meter for tracing and metrics collection.
/// </para>
/// <example>
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(builder => builder.AddToolboxInstrumentation())
///     .WithMetrics(builder => builder.AddToolboxInstrumentation());
/// </code>
/// </example>
/// </remarks>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds Toolbox instrumentation to the <see cref="TracerProviderBuilder"/>.
    /// </summary>
    /// <param name="builder">The tracer provider builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// This method adds the Toolbox activity source to the tracer provider,
    /// enabling distributed tracing for all Toolbox services.
    /// </remarks>
    public static TracerProviderBuilder AddToolboxInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddSource(ToolboxActivitySource.Name);
    }

    /// <summary>
    /// Adds Toolbox instrumentation to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder">The meter provider builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// This method adds the Toolbox meter to the meter provider,
    /// enabling metrics collection for all Toolbox services.
    /// </remarks>
    public static MeterProviderBuilder AddToolboxInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(ToolboxMeter.Name);
    }

    /// <summary>
    /// Adds Toolbox OpenTelemetry configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method registers telemetry options with default values.
    /// Use the overload with <see cref="Action{T}"/> to customize options.
    /// </remarks>
    public static IServiceCollection AddToolboxOpenTelemetry(this IServiceCollection services)
    {
        return services.AddToolboxOpenTelemetry(_ => { });
    }

    /// <summary>
    /// Adds Toolbox OpenTelemetry configuration to the service collection with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An action to configure telemetry options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is <c>null</c>.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddToolboxOpenTelemetry(options =>
    /// {
    ///     options.EnableTracing = true;
    ///     options.EnableMetrics = true;
    ///     options.OtlpEndpoint = "http://localhost:4317";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddToolboxOpenTelemetry(
        this IServiceCollection services,
        Action<ToolboxTelemetryOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new ToolboxTelemetryOptions();
        configureOptions(options);

        services.Configure(configureOptions);

        var otelBuilder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: options.ServiceName,
                    serviceVersion: options.ServiceVersion));

        if (options.EnableTracing)
        {
            otelBuilder.WithTracing(tracing =>
            {
                tracing.AddToolboxInstrumentation();

                if (options.EnableConsoleExport)
                {
                    tracing.AddConsoleExporter();
                }
            });
        }

        if (options.EnableMetrics)
        {
            otelBuilder.WithMetrics(metrics =>
            {
                metrics.AddToolboxInstrumentation();

                if (options.EnableConsoleExport)
                {
                    metrics.AddConsoleExporter();
                }
            });
        }

        return services;
    }

    /// <summary>
    /// Adds Toolbox OpenTelemetry configuration from <see cref="IConfiguration"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to bind options from.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// This method binds options from the <c>Toolbox:Telemetry</c> configuration section.
    /// </remarks>
    public static IServiceCollection AddToolboxOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new ToolboxTelemetryOptions();
        configuration.GetSection(ToolboxTelemetryOptions.SectionName).Bind(options);

        return services.AddToolboxOpenTelemetry(opt =>
        {
            opt.EnableTracing = options.EnableTracing;
            opt.EnableMetrics = options.EnableMetrics;
            opt.EnableConsoleExport = options.EnableConsoleExport;
            opt.OtlpEndpoint = options.OtlpEndpoint;
            opt.ServiceName = options.ServiceName;
            opt.ServiceVersion = options.ServiceVersion;
        });
    }
}
