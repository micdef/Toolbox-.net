// @file SftpFileTransferService.cs
// @brief SFTP file transfer service implementation
// @details Implements IFileTransferService using SSH.NET
// @note Supports password and private key authentication

using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Common;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.FileTransfer;

/// <summary>
/// File transfer service implementation using SFTP protocol.
/// </summary>
/// <remarks>
/// <para>
/// This service provides secure file upload and download capabilities using the SFTP protocol.
/// It supports both password and private key authentication.
/// </para>
/// <para>
/// Uses SSH.NET library for SFTP operations.
/// </para>
/// </remarks>
/// <seealso cref="IFileTransferService"/>
public sealed class SftpFileTransferService : BaseAsyncDisposableService, IFileTransferService
{
    // The SFTP client instance
    private readonly SftpClient _client;

    // The service options
    private readonly FileTransferOptions _options;

    // The logger instance
    private readonly ILogger<SftpFileTransferService> _logger;

    // Lock for connection management
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="SftpFileTransferService"/> class.
    /// </summary>
    /// <param name="options">The file transfer options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when host or username is empty.</exception>
    public SftpFileTransferService(
        IOptions<FileTransferOptions> options,
        ILogger<SftpFileTransferService> logger)
        : base("SftpFileTransferService", logger)
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

        var connectionInfo = CreateConnectionInfo();
        _client = new SftpClient(connectionInfo);
        _client.OperationTimeout = _options.OperationTimeout;
        _client.BufferSize = (uint)_options.BufferSize;

        _logger.LogDebug(
            "SftpFileTransferService initialized for {Host}:{Port}",
            _options.Host,
            _options.GetEffectivePort());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SftpFileTransferService"/> class.
    /// </summary>
    /// <param name="options">The file transfer options.</param>
    /// <param name="logger">The logger instance.</param>
    public SftpFileTransferService(
        FileTransferOptions options,
        ILogger<SftpFileTransferService> logger)
        : this(Microsoft.Extensions.Options.Options.Create(options), logger)
    {
    }

    /// <inheritdoc />
    public void UploadOne(string localPath, string remotePath, bool overwrite = true)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(localPath);
        ArgumentNullException.ThrowIfNull(remotePath);

        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException("Local file not found.", localPath);
        }

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            EnsureConnected();

            if (!overwrite && _client.Exists(remotePath))
            {
                _logger.LogDebug("Skipping upload, remote file exists: {RemotePath}", remotePath);
                return;
            }

            // Create remote directory if needed
            if (_options.AutoCreateDirectory)
            {
                var remoteDir = GetDirectoryPath(remotePath);
                if (!string.IsNullOrEmpty(remoteDir))
                {
                    CreateDirectoryRecursive(remoteDir);
                }
            }

            using var fileStream = File.OpenRead(localPath);
            var fileSize = fileStream.Length;
            _client.UploadFile(fileStream, remotePath, overwrite);

            _logger.LogDebug("Uploaded {LocalPath} to {RemotePath}", localPath, remotePath);
            RecordOperation("UploadOne", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordFileUpload(ServiceName, "SFTP", _options.Host, fileSize);
        }
        catch (SftpPathNotFoundException ex)
        {
            _logger.LogError(ex, "Remote path not found: {RemotePath}", remotePath);
            ToolboxMeter.RecordFileTransferError(ServiceName, "SFTP", _options.Host, "upload", ex.GetType().Name);
            throw new IOException($"Remote path not found: {remotePath}", ex);
        }
        catch (SshException ex)
        {
            _logger.LogError(ex, "Failed to upload {LocalPath} to {RemotePath}", localPath, remotePath);
            ToolboxMeter.RecordFileTransferError(ServiceName, "SFTP", _options.Host, "upload", ex.GetType().Name);
            throw new IOException($"Failed to upload file: {ex.Message}", ex);
        }
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

            if (!overwrite && _client.Exists(remotePath))
            {
                _logger.LogDebug("Skipping upload, remote file exists: {RemotePath}", remotePath);
                return;
            }

            // Create remote directory if needed
            if (_options.AutoCreateDirectory)
            {
                var remoteDir = GetDirectoryPath(remotePath);
                if (!string.IsNullOrEmpty(remoteDir))
                {
                    await Task.Run(() => CreateDirectoryRecursive(remoteDir), cancellationToken);
                }
            }

            await using var fileStream = File.OpenRead(localPath);
            var fileSize = fileStream.Length;
            await Task.Run(() => _client.UploadFile(fileStream, remotePath, overwrite), cancellationToken);

            _logger.LogDebug("Uploaded {LocalPath} to {RemotePath}", localPath, remotePath);
            RecordOperation("UploadOneAsync", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordFileUpload(ServiceName, "SFTP", _options.Host, fileSize);
        }
        catch (SftpPathNotFoundException ex)
        {
            _logger.LogError(ex, "Remote path not found: {RemotePath}", remotePath);
            ToolboxMeter.RecordFileTransferError(ServiceName, "SFTP", _options.Host, "upload", ex.GetType().Name);
            throw new IOException($"Remote path not found: {remotePath}", ex);
        }
        catch (SshException ex)
        {
            _logger.LogError(ex, "Failed to upload {LocalPath} to {RemotePath}", localPath, remotePath);
            ToolboxMeter.RecordFileTransferError(ServiceName, "SFTP", _options.Host, "upload", ex.GetType().Name);
            throw new IOException($"Failed to upload file: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public int UploadBatch(IEnumerable<(string LocalPath, string RemotePath)> files, bool overwrite = true)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(files);

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var fileList = files.ToList();
        var successCount = 0;
        var exceptions = new List<Exception>();

        EnsureConnected();

        foreach (var (localPath, remotePath) in fileList)
        {
            try
            {
                UploadOne(localPath, remotePath, overwrite);
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

        RecordOperation("UploadBatchAsync", sw.ElapsedMilliseconds);

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
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(remotePath);
        ArgumentNullException.ThrowIfNull(localPath);

        if (!overwrite && File.Exists(localPath))
        {
            _logger.LogDebug("Skipping download, local file exists: {LocalPath}", localPath);
            return;
        }

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            EnsureConnected();

            if (!_client.Exists(remotePath))
            {
                throw new FileNotFoundException($"Remote file not found: {remotePath}", remotePath);
            }

            // Ensure local directory exists
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }

            using var fileStream = File.Create(localPath);
            _client.DownloadFile(remotePath, fileStream);
            var fileSize = fileStream.Length;

            _logger.LogDebug("Downloaded {RemotePath} to {LocalPath}", remotePath, localPath);
            RecordOperation("DownloadOne", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordFileDownload(ServiceName, "SFTP", _options.Host, fileSize);
        }
        catch (SftpPathNotFoundException ex)
        {
            ToolboxMeter.RecordFileTransferError(ServiceName, "SFTP", _options.Host, "download", ex.GetType().Name);
            throw new FileNotFoundException($"Remote file not found: {remotePath}", remotePath, ex);
        }
        catch (SshException ex)
        {
            _logger.LogError(ex, "Failed to download {RemotePath} to {LocalPath}", remotePath, localPath);
            ToolboxMeter.RecordFileTransferError(ServiceName, "SFTP", _options.Host, "download", ex.GetType().Name);
            throw new IOException($"Failed to download file: {ex.Message}", ex);
        }
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

            var exists = await Task.Run(() => _client.Exists(remotePath), cancellationToken);
            if (!exists)
            {
                throw new FileNotFoundException($"Remote file not found: {remotePath}", remotePath);
            }

            // Ensure local directory exists
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }

            await using var fileStream = File.Create(localPath);
            await Task.Run(() => _client.DownloadFile(remotePath, fileStream), cancellationToken);
            var fileSize = fileStream.Length;

            _logger.LogDebug("Downloaded {RemotePath} to {LocalPath}", remotePath, localPath);
            RecordOperation("DownloadOneAsync", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordFileDownload(ServiceName, "SFTP", _options.Host, fileSize);
        }
        catch (SftpPathNotFoundException ex)
        {
            ToolboxMeter.RecordFileTransferError(ServiceName, "SFTP", _options.Host, "download", ex.GetType().Name);
            throw new FileNotFoundException($"Remote file not found: {remotePath}", remotePath, ex);
        }
        catch (SshException ex)
        {
            _logger.LogError(ex, "Failed to download {RemotePath} to {LocalPath}", remotePath, localPath);
            ToolboxMeter.RecordFileTransferError(ServiceName, "SFTP", _options.Host, "download", ex.GetType().Name);
            throw new IOException($"Failed to download file: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public int DownloadBatch(IEnumerable<(string RemotePath, string LocalPath)> files, bool overwrite = true)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(files);

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var fileList = files.ToList();
        var successCount = 0;
        var exceptions = new List<Exception>();

        EnsureConnected();

        foreach (var (remotePath, localPath) in fileList)
        {
            try
            {
                DownloadOne(remotePath, localPath, overwrite);
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

        RecordOperation("DownloadBatchAsync", sw.ElapsedMilliseconds);

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
            if (_client.IsConnected)
            {
                _client.Disconnect();
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
    /// Creates the SSH connection info based on the configured options.
    /// </summary>
    /// <returns>The connection info.</returns>
    private ConnectionInfo CreateConnectionInfo()
    {
        var authMethods = new List<AuthenticationMethod>();

        // Private key authentication
        if (!string.IsNullOrEmpty(_options.PrivateKeyPath) || !string.IsNullOrEmpty(_options.PrivateKeyContent))
        {
            PrivateKeyFile keyFile;

            if (!string.IsNullOrEmpty(_options.PrivateKeyContent))
            {
                using var keyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_options.PrivateKeyContent));
                keyFile = string.IsNullOrEmpty(_options.PrivateKeyPassphrase)
                    ? new PrivateKeyFile(keyStream)
                    : new PrivateKeyFile(keyStream, _options.PrivateKeyPassphrase);
            }
            else
            {
                keyFile = string.IsNullOrEmpty(_options.PrivateKeyPassphrase)
                    ? new PrivateKeyFile(_options.PrivateKeyPath!)
                    : new PrivateKeyFile(_options.PrivateKeyPath!, _options.PrivateKeyPassphrase);
            }

            authMethods.Add(new PrivateKeyAuthenticationMethod(_options.Username, keyFile));
        }

        // Password authentication
        if (!string.IsNullOrEmpty(_options.Password))
        {
            authMethods.Add(new PasswordAuthenticationMethod(_options.Username, _options.Password));
        }

        if (authMethods.Count == 0)
        {
            throw new ArgumentException("No authentication method configured. Provide either a password or private key.");
        }

        return new ConnectionInfo(
            _options.Host,
            _options.GetEffectivePort(),
            _options.Username,
            authMethods.ToArray())
        {
            Timeout = _options.ConnectionTimeout
        };
    }

    /// <summary>
    /// Ensures the client is connected to the server.
    /// </summary>
    private void EnsureConnected()
    {
        if (_client.IsConnected)
        {
            return;
        }

        _connectionLock.Wait();
        try
        {
            if (!_client.IsConnected)
            {
                _client.Connect();
                _logger.LogDebug("Connected to SFTP server {Host}:{Port}", _options.Host, _options.GetEffectivePort());
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Ensures the client is connected to the server asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            return;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (!_client.IsConnected)
            {
                await Task.Run(() => _client.Connect(), cancellationToken);
                _logger.LogDebug("Connected to SFTP server {Host}:{Port}", _options.Host, _options.GetEffectivePort());
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Creates a directory recursively on the remote server.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    private void CreateDirectoryRecursive(string path)
    {
        var current = string.Empty;

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = $"{current}/{segment}";

            if (!_client.Exists(current))
            {
                _client.CreateDirectory(current);
            }
        }
    }

    /// <summary>
    /// Gets the directory portion of a path.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <returns>The directory path.</returns>
    private static string GetDirectoryPath(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        return lastSlash > 0 ? path[..lastSlash] : string.Empty;
    }
}
