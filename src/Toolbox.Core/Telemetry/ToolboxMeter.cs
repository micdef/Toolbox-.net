// @file ToolboxMeter.cs
// @brief Shared Meter for metrics collection
// @details Provides a singleton Meter and pre-defined instruments for Toolbox services
// @note Use this meter for creating counters, histograms, and gauges in service implementations

namespace Toolbox.Core.Telemetry;

/// <summary>
/// Provides a shared <see cref="Meter"/> and pre-defined instruments for metrics collection.
/// </summary>
/// <remarks>
/// <para>
/// This class exposes a singleton <see cref="Meter"/> along with commonly used instruments
/// for tracking service operations, durations, and lifecycle events.
/// </para>
/// <para>
/// The meter and instruments are lazily initialized and thread-safe.
/// </para>
/// </remarks>
public static class ToolboxMeter
{
    // Lazy initializer for thread-safe singleton meter
    private static readonly Lazy<Meter> LazyMeter = new(
        () => new Meter(TelemetryConstants.MeterName, TelemetryConstants.Version),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Lazy initializer for operation counter
    private static readonly Lazy<Counter<long>> LazyOperationCounter = new(
        () => Instance.CreateCounter<long>(
            TelemetryConstants.Metrics.OperationCount,
            unit: "{operations}",
            description: "Number of operations performed by Toolbox services"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Lazy initializer for operation duration histogram
    private static readonly Lazy<Histogram<double>> LazyOperationDuration = new(
        () => Instance.CreateHistogram<double>(
            TelemetryConstants.Metrics.OperationDuration,
            unit: "ms",
            description: "Duration of operations performed by Toolbox services"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Lazy initializer for disposal counter
    private static readonly Lazy<Counter<long>> LazyDisposalCounter = new(
        () => Instance.CreateCounter<long>(
            TelemetryConstants.Metrics.DisposalCount,
            unit: "{disposals}",
            description: "Number of service disposal events"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Lazy initializer for active instances gauge
    private static readonly Lazy<UpDownCounter<long>> LazyActiveInstances = new(
        () => Instance.CreateUpDownCounter<long>(
            TelemetryConstants.Metrics.ActiveInstances,
            unit: "{instances}",
            description: "Number of active Toolbox service instances"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Lazy initializer for crypto encrypt counter
    private static readonly Lazy<Counter<long>> LazyCryptoEncryptCounter = new(
        () => Instance.CreateCounter<long>(
            TelemetryConstants.Metrics.CryptoEncryptCount,
            unit: "{operations}",
            description: "Number of encryption operations"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Lazy initializer for crypto decrypt counter
    private static readonly Lazy<Counter<long>> LazyCryptoDecryptCounter = new(
        () => Instance.CreateCounter<long>(
            TelemetryConstants.Metrics.CryptoDecryptCount,
            unit: "{operations}",
            description: "Number of decryption operations"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Lazy initializer for crypto data size histogram
    private static readonly Lazy<Histogram<long>> LazyCryptoDataSize = new(
        () => Instance.CreateHistogram<long>(
            TelemetryConstants.Metrics.CryptoDataSize,
            unit: "By",
            description: "Size of encrypted/decrypted data in bytes"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Lazy initializer for file transfer upload counter
    private static readonly Lazy<Counter<long>> LazyFileTransferUploadCounter = new(
        () => Instance.CreateCounter<long>(
            TelemetryConstants.Metrics.FileTransferUploadCount,
            unit: "{files}",
            description: "Number of file upload operations"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Lazy initializer for file transfer download counter
    private static readonly Lazy<Counter<long>> LazyFileTransferDownloadCounter = new(
        () => Instance.CreateCounter<long>(
            TelemetryConstants.Metrics.FileTransferDownloadCount,
            unit: "{files}",
            description: "Number of file download operations"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Lazy initializer for file transfer size histogram
    private static readonly Lazy<Histogram<long>> LazyFileTransferSize = new(
        () => Instance.CreateHistogram<long>(
            TelemetryConstants.Metrics.FileTransferSize,
            unit: "By",
            description: "Size of transferred files in bytes"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Lazy initializer for file transfer error counter
    private static readonly Lazy<Counter<long>> LazyFileTransferErrorCounter = new(
        () => Instance.CreateCounter<long>(
            TelemetryConstants.Metrics.FileTransferErrorCount,
            unit: "{errors}",
            description: "Number of file transfer errors"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the shared <see cref="Meter"/> instance for Toolbox services.
    /// </summary>
    /// <value>The singleton meter instance.</value>
    public static Meter Instance => LazyMeter.Value;

    /// <summary>
    /// Gets the name of the meter.
    /// </summary>
    /// <value>The meter name as defined in <see cref="TelemetryConstants"/>.</value>
    public static string Name => TelemetryConstants.MeterName;

    /// <summary>
    /// Gets the version of the meter.
    /// </summary>
    /// <value>The telemetry version string.</value>
    public static string Version => TelemetryConstants.Version;

    /// <summary>
    /// Gets the counter for tracking operation counts.
    /// </summary>
    /// <value>A counter instrument for operation events.</value>
    public static Counter<long> OperationCounter => LazyOperationCounter.Value;

    /// <summary>
    /// Gets the histogram for tracking operation durations.
    /// </summary>
    /// <value>A histogram instrument for operation timing.</value>
    public static Histogram<double> OperationDuration => LazyOperationDuration.Value;

    /// <summary>
    /// Gets the counter for tracking disposal events.
    /// </summary>
    /// <value>A counter instrument for disposal events.</value>
    public static Counter<long> DisposalCounter => LazyDisposalCounter.Value;

    /// <summary>
    /// Gets the up/down counter for tracking active service instances.
    /// </summary>
    /// <value>An up/down counter instrument for active instances.</value>
    public static UpDownCounter<long> ActiveInstances => LazyActiveInstances.Value;

    /// <summary>
    /// Records an operation with the specified service name.
    /// </summary>
    /// <param name="serviceName">The name of the service performing the operation.</param>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="durationMs">The duration of the operation in milliseconds.</param>
    public static void RecordOperation(string serviceName, string operationName, double durationMs)
    {
        var tags = new TagList
        {
            { TelemetryConstants.Attributes.ServiceName, serviceName },
            { TelemetryConstants.Attributes.OperationName, operationName }
        };

        OperationCounter.Add(1, tags);
        OperationDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Records a disposal event for the specified service.
    /// </summary>
    /// <param name="serviceName">The name of the disposed service.</param>
    /// <param name="reason">Optional reason for disposal.</param>
    public static void RecordDisposal(string serviceName, string? reason = null)
    {
        var tags = new TagList
        {
            { TelemetryConstants.Attributes.ServiceName, serviceName }
        };

        if (reason is not null)
        {
            tags.Add(TelemetryConstants.Attributes.DisposalReason, reason);
        }

        DisposalCounter.Add(1, tags);
        ActiveInstances.Add(-1, tags);
    }

    /// <summary>
    /// Increments the active instance count for the specified service.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    public static void IncrementActiveInstances(string serviceName)
    {
        var tags = new TagList
        {
            { TelemetryConstants.Attributes.ServiceName, serviceName }
        };

        ActiveInstances.Add(1, tags);
    }

    /// <summary>
    /// Records an encryption operation.
    /// </summary>
    /// <param name="serviceName">The name of the cryptography service.</param>
    /// <param name="algorithm">The encryption algorithm used.</param>
    /// <param name="dataSize">The size of encrypted data in bytes.</param>
    /// <param name="keySize">The key size in bits (optional).</param>
    public static void RecordEncryption(string serviceName, string algorithm, long dataSize, int? keySize = null)
    {
        var tags = new TagList
        {
            { TelemetryConstants.Attributes.ServiceName, serviceName },
            { TelemetryConstants.CryptoAttributes.Algorithm, algorithm }
        };

        if (keySize.HasValue)
        {
            tags.Add(TelemetryConstants.CryptoAttributes.KeySize, keySize.Value);
        }

        LazyCryptoEncryptCounter.Value.Add(1, tags);
        LazyCryptoDataSize.Value.Record(dataSize, tags);
    }

    /// <summary>
    /// Records a decryption operation.
    /// </summary>
    /// <param name="serviceName">The name of the cryptography service.</param>
    /// <param name="algorithm">The encryption algorithm used.</param>
    /// <param name="dataSize">The size of decrypted data in bytes.</param>
    /// <param name="keySize">The key size in bits (optional).</param>
    public static void RecordDecryption(string serviceName, string algorithm, long dataSize, int? keySize = null)
    {
        var tags = new TagList
        {
            { TelemetryConstants.Attributes.ServiceName, serviceName },
            { TelemetryConstants.CryptoAttributes.Algorithm, algorithm }
        };

        if (keySize.HasValue)
        {
            tags.Add(TelemetryConstants.CryptoAttributes.KeySize, keySize.Value);
        }

        LazyCryptoDecryptCounter.Value.Add(1, tags);
        LazyCryptoDataSize.Value.Record(dataSize, tags);
    }

    /// <summary>
    /// Records a file upload operation.
    /// </summary>
    /// <param name="serviceName">The name of the file transfer service.</param>
    /// <param name="protocol">The transfer protocol used.</param>
    /// <param name="host">The remote host.</param>
    /// <param name="fileSize">The size of the uploaded file in bytes.</param>
    public static void RecordFileUpload(string serviceName, string protocol, string host, long fileSize)
    {
        var tags = new TagList
        {
            { TelemetryConstants.Attributes.ServiceName, serviceName },
            { TelemetryConstants.FileTransferAttributes.Protocol, protocol },
            { TelemetryConstants.FileTransferAttributes.Host, host },
            { TelemetryConstants.FileTransferAttributes.Direction, "upload" }
        };

        LazyFileTransferUploadCounter.Value.Add(1, tags);
        LazyFileTransferSize.Value.Record(fileSize, tags);
    }

    /// <summary>
    /// Records a file download operation.
    /// </summary>
    /// <param name="serviceName">The name of the file transfer service.</param>
    /// <param name="protocol">The transfer protocol used.</param>
    /// <param name="host">The remote host.</param>
    /// <param name="fileSize">The size of the downloaded file in bytes.</param>
    public static void RecordFileDownload(string serviceName, string protocol, string host, long fileSize)
    {
        var tags = new TagList
        {
            { TelemetryConstants.Attributes.ServiceName, serviceName },
            { TelemetryConstants.FileTransferAttributes.Protocol, protocol },
            { TelemetryConstants.FileTransferAttributes.Host, host },
            { TelemetryConstants.FileTransferAttributes.Direction, "download" }
        };

        LazyFileTransferDownloadCounter.Value.Add(1, tags);
        LazyFileTransferSize.Value.Record(fileSize, tags);
    }

    /// <summary>
    /// Records a file transfer error.
    /// </summary>
    /// <param name="serviceName">The name of the file transfer service.</param>
    /// <param name="protocol">The transfer protocol used.</param>
    /// <param name="host">The remote host.</param>
    /// <param name="direction">The transfer direction (upload/download).</param>
    /// <param name="errorType">The type of error.</param>
    public static void RecordFileTransferError(string serviceName, string protocol, string host, string direction, string errorType)
    {
        var tags = new TagList
        {
            { TelemetryConstants.Attributes.ServiceName, serviceName },
            { TelemetryConstants.FileTransferAttributes.Protocol, protocol },
            { TelemetryConstants.FileTransferAttributes.Host, host },
            { TelemetryConstants.FileTransferAttributes.Direction, direction },
            { TelemetryConstants.Attributes.ErrorType, errorType }
        };

        LazyFileTransferErrorCounter.Value.Add(1, tags);
    }
}
