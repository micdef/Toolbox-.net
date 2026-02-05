// @file ToolboxOptions.cs
// @brief Configuration options for Toolbox services
// @details Defines configuration options for core Toolbox functionality
// @note Configure these options via IConfiguration or direct instantiation

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for Toolbox core services.
/// </summary>
/// <remarks>
/// These options can be configured via <c>appsettings.json</c>:
/// <code>
/// {
///   "Toolbox": {
///     "EnableDetailedTelemetry": true,
///     "ServicePrefix": "MyApp"
///   }
/// }
/// </code>
/// </remarks>
public sealed class ToolboxOptions
{
    /// <summary>
    /// The configuration section name for Toolbox options.
    /// </summary>
    public const string SectionName = "Toolbox";

    /// <summary>
    /// Gets or sets a value indicating whether detailed telemetry should be enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable detailed telemetry including all operations;
    /// <c>false</c> for minimal telemetry. Defaults to <c>false</c>.
    /// </value>
    /// <remarks>
    /// When enabled, additional spans and metrics are recorded for internal operations.
    /// This may increase telemetry volume but provides better debugging capabilities.
    /// </remarks>
    public bool EnableDetailedTelemetry { get; set; }

    /// <summary>
    /// Gets or sets the prefix to use for service names in telemetry.
    /// </summary>
    /// <value>
    /// A string prefix prepended to service names. Defaults to <c>null</c> (no prefix).
    /// </value>
    /// <remarks>
    /// Use this to distinguish services from different applications using the same Toolbox library.
    /// </remarks>
    public string? ServicePrefix { get; set; }

    /// <summary>
    /// Gets or sets the default timeout for async disposal operations.
    /// </summary>
    /// <value>
    /// The timeout duration. Defaults to 30 seconds.
    /// </value>
    public TimeSpan AsyncDisposalTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
