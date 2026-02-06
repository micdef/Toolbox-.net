// @file FileTransferOptions.cs
// @brief Configuration options for file transfer services
// @details Contains connection and authentication settings
// @note Supports password and key-based authentication

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for file transfer services.
/// </summary>
/// <remarks>
/// <para>
/// These options configure the connection to FTP/SFTP servers.
/// </para>
/// <para>
/// For SFTP, you can use either password authentication or private key authentication.
/// </para>
/// </remarks>
public sealed class FileTransferOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Toolbox:FileTransfer";

    /// <summary>
    /// Gets or sets the server hostname or IP address.
    /// </summary>
    /// <value>The server address.</value>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the server port.
    /// </summary>
    /// <value>The port number. Default is 21 for FTP, 22 for SFTP.</value>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the username for authentication.
    /// </summary>
    /// <value>The username.</value>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password for authentication.
    /// </summary>
    /// <value>The password, or <c>null</c> for key-based authentication.</value>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the private key file path for SFTP key-based authentication.
    /// </summary>
    /// <value>The path to the private key file, or <c>null</c> for password authentication.</value>
    public string? PrivateKeyPath { get; set; }

    /// <summary>
    /// Gets or sets the private key content for SFTP key-based authentication.
    /// </summary>
    /// <value>The private key content as a string, or <c>null</c>.</value>
    public string? PrivateKeyContent { get; set; }

    /// <summary>
    /// Gets or sets the passphrase for the private key.
    /// </summary>
    /// <value>The passphrase, or <c>null</c> if the key is not encrypted.</value>
    public string? PrivateKeyPassphrase { get; set; }

    /// <summary>
    /// Gets or sets the file transfer protocol to use.
    /// </summary>
    /// <value>The protocol. Default is <see cref="FileTransferProtocol.Sftp"/>.</value>
    public FileTransferProtocol Protocol { get; set; } = FileTransferProtocol.Sftp;

    /// <summary>
    /// Gets or sets the connection timeout.
    /// </summary>
    /// <value>The timeout duration. Default is 30 seconds.</value>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the operation timeout for read/write operations.
    /// </summary>
    /// <value>The timeout duration. Default is 60 seconds.</value>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets a value indicating whether to automatically create remote directories.
    /// </summary>
    /// <value><c>true</c> to auto-create directories; otherwise, <c>false</c>. Default is <c>true</c>.</value>
    public bool AutoCreateDirectory { get; set; } = true;

    /// <summary>
    /// Gets or sets the buffer size for file transfers.
    /// </summary>
    /// <value>The buffer size in bytes. Default is 32 KB.</value>
    public int BufferSize { get; set; } = 32 * 1024;

    /// <summary>
    /// Gets the default port for the current protocol.
    /// </summary>
    /// <returns>The default port number.</returns>
    public int GetDefaultPort() => Protocol switch
    {
        FileTransferProtocol.Ftp => 21,
        FileTransferProtocol.Ftps => 990,
        FileTransferProtocol.Sftp => 22,
        _ => 21
    };

    /// <summary>
    /// Gets the effective port, using the default if not explicitly set.
    /// </summary>
    /// <returns>The port number to use.</returns>
    public int GetEffectivePort() => Port > 0 ? Port : GetDefaultPort();
}
