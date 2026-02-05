// @file ToolboxTelemetryOptions.cs
// @brief Configuration options for OpenTelemetry integration
// @details Defines options for configuring tracing and metrics export
// @note Configure these options to customize telemetry behavior

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for OpenTelemetry integration in Toolbox.
/// </summary>
/// <remarks>
/// These options can be configured via <c>appsettings.json</c>:
/// <code>
/// {
///   "Toolbox:Telemetry": {
///     "EnableTracing": true,
///     "EnableMetrics": true,
///     "OtlpEndpoint": "http://localhost:4317"
///   }
/// }
/// </code>
/// </remarks>
public sealed class ToolboxTelemetryOptions
{
    /// <summary>
    /// The configuration section name for telemetry options.
    /// </summary>
    public const string SectionName = "Toolbox:Telemetry";

    /// <summary>
    /// Gets or sets a value indicating whether distributed tracing is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable tracing; <c>false</c> to disable. Defaults to <c>true</c>.
    /// </value>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether metrics collection is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable metrics; <c>false</c> to disable. Defaults to <c>true</c>.
    /// </value>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether console export is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to export telemetry to console; <c>false</c> to disable. Defaults to <c>false</c>.
    /// </value>
    /// <remarks>
    /// Enable this for debugging purposes only. Not recommended for production.
    /// </remarks>
    public bool EnableConsoleExport { get; set; }

    /// <summary>
    /// Gets or sets the OTLP endpoint for telemetry export.
    /// </summary>
    /// <value>
    /// The OTLP endpoint URL. Defaults to <c>null</c> (OTLP export disabled).
    /// </value>
    /// <remarks>
    /// Example: <c>http://localhost:4317</c> for a local OTLP collector.
    /// </remarks>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the service name for telemetry identification.
    /// </summary>
    /// <value>
    /// The service name used in telemetry. Defaults to <c>"Toolbox"</c>.
    /// </value>
    public string ServiceName { get; set; } = "Toolbox";

    /// <summary>
    /// Gets or sets the service version for telemetry identification.
    /// </summary>
    /// <value>
    /// The service version string. Defaults to <c>"1.0.0"</c>.
    /// </value>
    public string ServiceVersion { get; set; } = "1.0.0";
}
