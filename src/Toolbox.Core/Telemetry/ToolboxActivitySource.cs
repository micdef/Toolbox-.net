// @file ToolboxActivitySource.cs
// @brief Shared ActivitySource for distributed tracing
// @details Provides a singleton ActivitySource for all Toolbox services
// @note Use this source for creating spans in service implementations

namespace Toolbox.Core.Telemetry;

/// <summary>
/// Provides a shared <see cref="ActivitySource"/> for distributed tracing across Toolbox services.
/// </summary>
/// <remarks>
/// <para>
/// This class exposes a singleton <see cref="ActivitySource"/> that should be used by all
/// Toolbox services for creating trace spans. Using a shared source ensures consistent
/// naming and simplifies OpenTelemetry configuration.
/// </para>
/// <para>
/// The activity source is lazily initialized and thread-safe.
/// </para>
/// </remarks>
public static class ToolboxActivitySource
{
    // Lazy initializer for thread-safe singleton
    private static readonly Lazy<ActivitySource> LazySource = new(
        () => new ActivitySource(TelemetryConstants.ActivitySourceName, TelemetryConstants.Version),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the shared <see cref="ActivitySource"/> instance for Toolbox services.
    /// </summary>
    /// <value>The singleton activity source instance.</value>
    /// <remarks>
    /// This source should be added to OpenTelemetry configuration using:
    /// <code>
    /// builder.AddSource(ToolboxActivitySource.Instance.Name);
    /// </code>
    /// </remarks>
    public static ActivitySource Instance => LazySource.Value;

    /// <summary>
    /// Gets the name of the activity source.
    /// </summary>
    /// <value>The activity source name as defined in <see cref="TelemetryConstants"/>.</value>
    public static string Name => TelemetryConstants.ActivitySourceName;

    /// <summary>
    /// Gets the version of the activity source.
    /// </summary>
    /// <value>The telemetry version string.</value>
    public static string Version => TelemetryConstants.Version;

    /// <summary>
    /// Starts a new activity with the specified name.
    /// </summary>
    /// <param name="name">The name of the activity (typically the operation name).</param>
    /// <param name="kind">The kind of activity. Defaults to <see cref="ActivityKind.Internal"/>.</param>
    /// <returns>
    /// The started <see cref="Activity"/> if there is a listener; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// This is a convenience method that wraps <see cref="ActivitySource.StartActivity(string, ActivityKind)"/>.
    /// The returned activity should be disposed when the operation completes.
    /// </remarks>
    public static Activity? StartActivity(
        [CallerMemberName] string name = "",
        ActivityKind kind = ActivityKind.Internal)
    {
        return Instance.StartActivity(name, kind);
    }
}
