// @file IFileTransferService.cs
// @brief Interface for file transfer services (FTP/SFTP)
// @details Defines contract for uploading and downloading files
// @note Supports both single file and batch operations

namespace Toolbox.Core.Abstractions.Services;

/// <summary>
/// Defines the contract for file transfer services supporting FTP and SFTP protocols.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides methods for uploading and downloading files
/// to/from remote servers using FTP or SFTP protocols.
/// </para>
/// <para>
/// All methods support both synchronous and asynchronous operations.
/// </para>
/// </remarks>
public interface IFileTransferService : IInstrumentedService, IAsyncDisposableService
{
    /// <summary>
    /// Uploads a single file to the remote server.
    /// </summary>
    /// <param name="localPath">The local file path to upload.</param>
    /// <param name="remotePath">The remote destination path.</param>
    /// <param name="overwrite">Whether to overwrite existing files.</param>
    /// <exception cref="ArgumentNullException">Thrown when paths are null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when local file doesn't exist.</exception>
    /// <exception cref="IOException">Thrown when transfer fails.</exception>
    void UploadOne(string localPath, string remotePath, bool overwrite = true);

    /// <summary>
    /// Uploads a single file to the remote server asynchronously.
    /// </summary>
    /// <param name="localPath">The local file path to upload.</param>
    /// <param name="remotePath">The remote destination path.</param>
    /// <param name="overwrite">Whether to overwrite existing files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when paths are null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when local file doesn't exist.</exception>
    /// <exception cref="IOException">Thrown when transfer fails.</exception>
    Task UploadOneAsync(string localPath, string remotePath, bool overwrite = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads multiple files to the remote server.
    /// </summary>
    /// <param name="files">Collection of tuples containing (localPath, remotePath) pairs.</param>
    /// <param name="overwrite">Whether to overwrite existing files.</param>
    /// <returns>The number of files successfully uploaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files collection is null.</exception>
    /// <exception cref="AggregateException">Thrown when one or more transfers fail.</exception>
    int UploadBatch(IEnumerable<(string LocalPath, string RemotePath)> files, bool overwrite = true);

    /// <summary>
    /// Uploads multiple files to the remote server asynchronously.
    /// </summary>
    /// <param name="files">Collection of tuples containing (localPath, remotePath) pairs.</param>
    /// <param name="overwrite">Whether to overwrite existing files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the number of files successfully uploaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files collection is null.</exception>
    /// <exception cref="AggregateException">Thrown when one or more transfers fail.</exception>
    Task<int> UploadBatchAsync(IEnumerable<(string LocalPath, string RemotePath)> files, bool overwrite = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a single file from the remote server.
    /// </summary>
    /// <param name="remotePath">The remote file path to download.</param>
    /// <param name="localPath">The local destination path.</param>
    /// <param name="overwrite">Whether to overwrite existing local files.</param>
    /// <exception cref="ArgumentNullException">Thrown when paths are null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when remote file doesn't exist.</exception>
    /// <exception cref="IOException">Thrown when transfer fails.</exception>
    void DownloadOne(string remotePath, string localPath, bool overwrite = true);

    /// <summary>
    /// Downloads a single file from the remote server asynchronously.
    /// </summary>
    /// <param name="remotePath">The remote file path to download.</param>
    /// <param name="localPath">The local destination path.</param>
    /// <param name="overwrite">Whether to overwrite existing local files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when paths are null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when remote file doesn't exist.</exception>
    /// <exception cref="IOException">Thrown when transfer fails.</exception>
    Task DownloadOneAsync(string remotePath, string localPath, bool overwrite = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads multiple files from the remote server.
    /// </summary>
    /// <param name="files">Collection of tuples containing (remotePath, localPath) pairs.</param>
    /// <param name="overwrite">Whether to overwrite existing local files.</param>
    /// <returns>The number of files successfully downloaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files collection is null.</exception>
    /// <exception cref="AggregateException">Thrown when one or more transfers fail.</exception>
    int DownloadBatch(IEnumerable<(string RemotePath, string LocalPath)> files, bool overwrite = true);

    /// <summary>
    /// Downloads multiple files from the remote server asynchronously.
    /// </summary>
    /// <param name="files">Collection of tuples containing (remotePath, localPath) pairs.</param>
    /// <param name="overwrite">Whether to overwrite existing local files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the number of files successfully downloaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files collection is null.</exception>
    /// <exception cref="AggregateException">Thrown when one or more transfers fail.</exception>
    Task<int> DownloadBatchAsync(IEnumerable<(string RemotePath, string LocalPath)> files, bool overwrite = true, CancellationToken cancellationToken = default);
}
