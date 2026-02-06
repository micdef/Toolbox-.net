// @file SmtpSecurityMode.cs
// @brief SMTP connection security modes
// @details Defines the security protocols for SMTP connections
// @note Auto mode will attempt the most secure connection available

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the security mode for SMTP connections.
/// </summary>
/// <remarks>
/// <para>
/// Choose the appropriate mode based on your SMTP server's requirements.
/// When in doubt, use <see cref="Auto"/> for automatic negotiation.
/// </para>
/// </remarks>
public enum SmtpSecurityMode
{
    /// <summary>
    /// Automatically determine the best security mode.
    /// Will attempt STARTTLS if available, otherwise use implicit SSL/TLS.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// No encryption (not recommended for production).
    /// Use only for local development or internal networks.
    /// </summary>
    None = 1,

    /// <summary>
    /// Use STARTTLS to upgrade an unencrypted connection.
    /// Standard for port 587.
    /// </summary>
    StartTls = 2,

    /// <summary>
    /// Use STARTTLS if available, otherwise continue unencrypted.
    /// Less secure than <see cref="StartTls"/> but more compatible.
    /// </summary>
    StartTlsWhenAvailable = 3,

    /// <summary>
    /// Use implicit SSL/TLS from the start.
    /// Standard for port 465.
    /// </summary>
    SslOnConnect = 4
}
