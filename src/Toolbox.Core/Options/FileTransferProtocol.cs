// @file FileTransferProtocol.cs
// @brief Enumeration of supported file transfer protocols
// @details Defines FTP and SFTP protocol options
// @note SFTP is recommended for secure transfers

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the file transfer protocol to use.
/// </summary>
/// <remarks>
/// SFTP provides encrypted transfers and is recommended for sensitive data.
/// </remarks>
public enum FileTransferProtocol
{
    /// <summary>
    /// File Transfer Protocol (unencrypted).
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> FTP transfers data in plain text. Use FTPS or SFTP for sensitive data.
    /// </remarks>
    Ftp,

    /// <summary>
    /// FTP over TLS/SSL (encrypted).
    /// </summary>
    /// <remarks>
    /// Provides encryption for FTP connections using TLS/SSL.
    /// </remarks>
    Ftps,

    /// <summary>
    /// SSH File Transfer Protocol (encrypted).
    /// </summary>
    /// <remarks>
    /// Provides secure file transfer over SSH. Recommended for sensitive data.
    /// </remarks>
    Sftp
}
