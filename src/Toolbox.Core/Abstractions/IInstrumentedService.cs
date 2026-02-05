// @file IInstrumentedService.cs
// @brief Interface for services with OpenTelemetry instrumentation
// @details Defines the contract for services that expose telemetry through ActivitySource and Meter
// @note Services implementing this interface participate in distributed tracing and metrics collection

namespace Toolbox.Core.Abstractions;

/// <summary>
/// Interface for services that expose OpenTelemetry instrumentation.
/// </summary>
/// <remarks>
/// <para>
/// Services implementing this interface provide access to their telemetry primitives,
/// enabling consistent observability across the application.
/// </para>
/// <para>
/// The <see cref="ActivitySource"/> is used for distributed tracing, while the
/// <see cref="Meter"/> is used for metrics collection.
/// </para>
/// </remarks>
public interface IInstrumentedService
{
    /// <summary>
    /// Gets the <see cref="System.Diagnostics.ActivitySource"/> for distributed tracing.
    /// </summary>
    /// <value>The activity source used to create spans for this service.</value>
    /// <remarks>
    /// Activities created from this source will be included in traces when
    /// OpenTelemetry tracing is configured.
    /// </remarks>
    ActivitySource ActivitySource { get; }

    /// <summary>
    /// Gets the <see cref="System.Diagnostics.Metrics.Meter"/> for metrics collection.
    /// </summary>
    /// <value>The meter used to create instruments for this service.</value>
    /// <remarks>
    /// Instruments created from this meter will report metrics when
    /// OpenTelemetry metrics is configured.
    /// </remarks>
    Meter Meter { get; }
}
