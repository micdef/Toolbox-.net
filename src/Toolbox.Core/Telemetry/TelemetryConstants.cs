// @file TelemetryConstants.cs
// @brief Constants for OpenTelemetry instrumentation
// @details Defines standard names and versions for telemetry sources
// @note These constants ensure consistent naming across all instrumented services

namespace Toolbox.Core.Telemetry;

/// <summary>
/// Constants used for OpenTelemetry instrumentation across Toolbox services.
/// </summary>
/// <remarks>
/// Using consistent names for activity sources and meters ensures proper
/// correlation and filtering in observability backends.
/// </remarks>
public static class TelemetryConstants
{
    /// <summary>
    /// The name of the root activity source for Toolbox services.
    /// </summary>
    public const string ActivitySourceName = "Toolbox.Core";

    /// <summary>
    /// The name of the root meter for Toolbox metrics.
    /// </summary>
    public const string MeterName = "Toolbox.Core";

    /// <summary>
    /// The current version of the telemetry instrumentation.
    /// </summary>
    /// <remarks>
    /// This version should be updated when breaking changes are made to
    /// telemetry semantics (attribute names, metric units, etc.).
    /// </remarks>
    public const string Version = "1.0.0";

    /// <summary>
    /// Standard attribute names for service telemetry.
    /// </summary>
    public static class Attributes
    {
        /// <summary>Attribute name for the service name.</summary>
        public const string ServiceName = "toolbox.service.name";

        /// <summary>Attribute name for operation names.</summary>
        public const string OperationName = "toolbox.operation.name";

        /// <summary>Attribute name for error types.</summary>
        public const string ErrorType = "toolbox.error.type";

        /// <summary>Attribute name for disposal reasons.</summary>
        public const string DisposalReason = "toolbox.disposal.reason";
    }

    /// <summary>
    /// Standard metric names for service instrumentation.
    /// </summary>
    public static class Metrics
    {
        /// <summary>Counter for service operations.</summary>
        public const string OperationCount = "toolbox.operations.count";

        /// <summary>Histogram for operation duration.</summary>
        public const string OperationDuration = "toolbox.operations.duration";

        /// <summary>Counter for disposal events.</summary>
        public const string DisposalCount = "toolbox.disposals.count";

        /// <summary>Gauge for active service instances.</summary>
        public const string ActiveInstances = "toolbox.instances.active";
    }
}
