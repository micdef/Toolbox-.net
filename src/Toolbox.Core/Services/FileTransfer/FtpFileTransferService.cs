// @file FtpFileTransferService.cs
// @brief FTP/FTPS file transfer service implementation
// @details Implements IFileTransferService using FluentFTP
// @note Supports both FTP and FTPS protocols

using FluentFTP;
using Microsoft.Extensions.Options;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;

namespace Toolbox.Core.Services.FileTransfer;

/// <summary>
/// File transfer service implementation using FTP/FTPS protocols.
/// </summary>
/// <remarks>
/// <para>
/// This service provides file upload and download capabilities using the FTP protocol.
/// It supports both plain FTP and FTPS (FTP over TLS/SSL).
/// </para>
/// <para>
/// Uses FluentFTP library for FTP operations.
/// </para>
/// </remarks>
/// <seealso cref="IFileTransferService"/>
public sealed class FtpFileTransferService : BaseAsyncDisposableService, IFileTransferService
{
    // The FTP client instance
    private readonly AsyncFtpClient _client;

    // The service options
    private readonly FileTransferOptions _options;

    // The logger instance
    private readonly ILogger<FtpFileTransferService> _logger;

    // Connection state
    private bool _isConnected;

    // Lock for connection management
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="FtpFileTransferService"/> class.
    /// </summary>
    /// <param name="options">The file transfer options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when host or username is empty.</exception>
    public FtpFileTransferService(
        IOptions<FileTransferOptions> options,
        ILogger<FtpFileTransferService> logger)
        : base("FtpFileTransferService", logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new ArgumentException("Host cannot be empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(_options.Username))
        {
            throw new ArgumentException("Username cannot be empty.", nameof(options));
        }

        _client = new AsyncFtpClient(
            _options.Host,
            _options.Username,
            _options.Password ?? string.Empty,
            _options.GetEffectivePort());

        ConfigureClient();

        _logger.LogDebug(
            "FtpFileTransferService initialized for {Host}:{Port} with protocol {Protocol}",
            _options.Host,
            _options.GetEffectivePort(),
            _options.Protocol);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FtpFileTransferService"/> class.
    /// </summary>
    /// <param name="options">The file transfer options.</param>
    /// <param name="logger">The logger instance.</param>
    public FtpFileTransferService(
        FileTransferOptions options,
        ILogger<FtpFileTransferService> logger)
        : this(Microsoft.Extensions.Options.Options.Create(options), logger)
    {
    }

    /// <inheritdoc />
    public void UploadOne(string localPath, string remotePath, bool overwrite = true)
    {
        UploadOneAsync(localPath, remotePath, overwrite).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task UploadOneAsync(string localPath, string remotePath, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(localPath);
        ArgumentNullException.ThrowIfNull(remotePath);

        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException("Local file not found.", localPath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            var existsMode = overwrite ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip;
            var createDir = _options.AutoCreateDirectory ? FtpLocalExists.Overwrite : FtpLocalExists.Skip;

            var status = await _client.UploadFile(
                localPath,
                remotePath,
                existsMode,
                _options.AutoCreateDirectory,
                FtpVerify.None,
                null,
                cancellationToken);

            if (status == FtpStatus.Failed)
            {
                throw new IOException($"Failed to upload file to {remotePath}");
            }

            _logger.LogDebug(
                "Uploaded {LocalPath} to {RemotePath} (status: {Status})",
                localPath,
                remotePath,
                status);

            RecordOperation("UploadOne", sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not ArgumentNullException and not FileNotFoundException and not OperationCanceledException and not ObjectDisposedException)
        {
            _logger.LogError(ex, "Failed to upload {LocalPath} to {RemotePath}", localPath, remotePath);
            throw new IOException($"Failed to upload file: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public int UploadBatch(IEnumerable<(string LocalPath, string RemotePath)> files, bool overwrite = true)
    {
        return UploadBatchAsync(files, overwrite).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<int> UploadBatchAsync(IEnumerable<(string LocalPath, string RemotePath)> files, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(files);

        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var fileList = files.ToList();
        var successCount = 0;
        var exceptions = new List<Exception>();

        await EnsureConnectedAsync(cancellationToken);

        foreach (var (localPath, remotePath) in fileList)
        {
            try
            {
                await UploadOneAsync(localPath, remotePath, overwrite, cancellationToken);
                successCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                exceptions.Add(new IOException($"Failed to upload {localPath}: {ex.Message}", ex));
            }
        }

        RecordOperation("UploadBatch", sw.ElapsedMilliseconds);

        if (exceptions.Count > 0)
        {
            _logger.LogWarning(
                "Batch upload completed with {SuccessCount}/{TotalCount} successes and {ErrorCount} errors",
                successCount,
                fileList.Count,
                exceptions.Count);

            if (successCount == 0)
            {
                throw new AggregateException("All file uploads failed.", exceptions);
            }
        }

        return successCount;
    }

    /// <inheritdoc />
    public void DownloadOne(string remotePath, string localPath, bool overwrite = true)
    {
        DownloadOneAsync(remotePath, localPath, overwrite).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task DownloadOneAsync(string remotePath, string localPath, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(remotePath);
        ArgumentNullException.ThrowIfNull(localPath);

        if (!overwrite && File.Exists(localPath))
        {
            _logger.LogDebug("Skipping download, local file exists: {LocalPath}", localPath);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            // Ensure local directory exists
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }

            var existsMode = overwrite ? FtpLocalExists.Overwrite : FtpLocalExists.Skip;

            var status = await _client.DownloadFile(
                localPath,
                remotePath,
                existsMode,
                FtpVerify.None,
                null,
                cancellationToken);

            if (status == FtpStatus.Failed)
            {
                throw new IOException($"Failed to download file from {remotePath}");
            }

            _logger.LogDebug(
                "Downloaded {RemotePath} to {LocalPath} (status: {Status})",
                remotePath,
                localPath,
                status);

            RecordOperation("DownloadOne", sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("550", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException($"Remote file not found: {remotePath}", remotePath, ex);
        }
        catch (Exception ex) when (ex is not ArgumentNullException and not FileNotFoundException and not OperationCanceledException and not ObjectDisposedException)
        {
            _logger.LogError(ex, "Failed to download {RemotePath} to {LocalPath}", remotePath, localPath);
            throw new IOException($"Failed to download file: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public int DownloadBatch(IEnumerable<(string RemotePath, string LocalPath)> files, bool overwrite = true)
    {
        return DownloadBatchAsync(files, overwrite).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<int> DownloadBatchAsync(IEnumerable<(string RemotePath, string LocalPath)> files, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(files);

        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var fileList = files.ToList();
        var successCount = 0;
        var exceptions = new List<Exception>();

        await EnsureConnectedAsync(cancellationToken);

        foreach (var (remotePath, localPath) in fileList)
        {
            try
            {
                await DownloadOneAsync(remotePath, localPath, overwrite, cancellationToken);
                successCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                exceptions.Add(new IOException($"Failed to download {remotePath}: {ex.Message}", ex));
            }
        }

        RecordOperation("DownloadBatch", sw.ElapsedMilliseconds);

        if (exceptions.Count > 0)
        {
            _logger.LogWarning(
                "Batch download completed with {SuccessCount}/{TotalCount} successes and {ErrorCount} errors",
                successCount,
                fileList.Count,
                exceptions.Count);

            if (successCount == 0)
            {
                throw new AggregateException("All file downloads failed.", exceptions);
            }
        }

        return successCount;
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected)
            {
                await _client.Disconnect(cancellationToken);
                _isConnected = false;
            }
        }
        finally
        {
            _connectionLock.Release();
        }

        _client.Dispose();
        _connectionLock.Dispose();
    }

    /// <summary>
    /// Configures the FTP client with the specified options.
    /// </summary>
    private void ConfigureClient()
    {
        _client.Config.ConnectTimeout = (int)_options.ConnectionTimeout.TotalMilliseconds;
        _client.Config.ReadTimeout = (int)_options.OperationTimeout.TotalMilliseconds;
        _client.Config.DataConnectionConnectTimeout = (int)_options.ConnectionTimeout.TotalMilliseconds;
        _client.Config.DataConnectionReadTimeout = (int)_options.OperationTimeout.TotalMilliseconds;
        _client.Config.LocalFileBufferSize = _options.BufferSize;

        if (_options.Protocol == FileTransferProtocol.Ftps)
        {
            _client.Config.EncryptionMode = FtpEncryptionMode.Explicit;
            _client.Config.ValidateAnyCertificate = true;
        }
    }

    /// <summary>
    /// Ensures the client is connected to the server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_isConnected && _client.IsConnected)
        {
            return;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (!_isConnected || !_client.IsConnected)
            {
                await _client.Connect(cancellationToken);
                _isConnected = true;
                _logger.LogDebug("Connected to FTP server {Host}:{Port}", _options.Host, _options.GetEffectivePort());
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}
