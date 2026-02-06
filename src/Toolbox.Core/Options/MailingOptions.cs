// @file MailingOptions.cs
// @brief Configuration options for the mailing service
// @details Contains SMTP server connection and authentication settings
// @note Supports both anonymous and authenticated connections

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for the SMTP mailing service.
/// </summary>
/// <remarks>
/// <para>
/// These options configure how the mailing service connects to an SMTP server.
/// Authentication is optional for servers that support anonymous relay.
/// </para>
/// <para>
/// For secure connections, use <see cref="SmtpSecurityMode.StartTls"/> (port 587)
/// or <see cref="SmtpSecurityMode.SslOnConnect"/> (port 465).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var options = new MailingOptions
/// {
///     Host = "smtp.example.com",
///     Port = 587,
///     SecurityMode = SmtpSecurityMode.StartTls,
///     Username = "user@example.com",
///     Password = "password"
/// };
/// </code>
/// </example>
public sealed class MailingOptions
{
    /// <summary>
    /// Gets or sets the SMTP server hostname or IP address.
    /// </summary>
    /// <value>The SMTP server host. Default is <c>"localhost"</c>.</value>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the SMTP server port.
    /// </summary>
    /// <value>
    /// The port number. Default is <c>25</c>.
    /// Common ports: 25 (unencrypted), 587 (STARTTLS), 465 (SSL/TLS).
    /// </value>
    public int Port { get; set; } = 25;

    /// <summary>
    /// Gets or sets the security mode for the SMTP connection.
    /// </summary>
    /// <value>The security mode. Default is <see cref="SmtpSecurityMode.Auto"/>.</value>
    public SmtpSecurityMode SecurityMode { get; set; } = SmtpSecurityMode.Auto;

    /// <summary>
    /// Gets or sets the username for SMTP authentication.
    /// </summary>
    /// <value>The username, or <c>null</c> for anonymous authentication.</value>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for SMTP authentication.
    /// </summary>
    /// <value>The password, or <c>null</c> for anonymous authentication.</value>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the OAuth2 access token for authentication.
    /// </summary>
    /// <value>The OAuth2 token, or <c>null</c> to use password authentication.</value>
    /// <remarks>
    /// When set, this takes precedence over password authentication.
    /// Useful for services like Gmail or Office 365 that require OAuth2.
    /// </remarks>
    public string? OAuth2AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the connection timeout.
    /// </summary>
    /// <value>The connection timeout. Default is 30 seconds.</value>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the operation timeout for sending emails.
    /// </summary>
    /// <value>The operation timeout. Default is 2 minutes.</value>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets whether to validate the server's SSL certificate.
    /// </summary>
    /// <value>
    /// <c>true</c> to validate certificates (recommended);
    /// <c>false</c> to accept all certificates (use only for testing).
    /// Default is <c>true</c>.
    /// </value>
    public bool ValidateCertificate { get; set; } = true;

    /// <summary>
    /// Gets or sets the default sender address to use when not specified in the message.
    /// </summary>
    /// <value>The default sender email address, or <c>null</c> if not set.</value>
    public EmailAddress? DefaultFrom { get; set; }

    /// <summary>
    /// Gets or sets the default reply-to address.
    /// </summary>
    /// <value>The default reply-to email address, or <c>null</c> if not set.</value>
    public EmailAddress? DefaultReplyTo { get; set; }

    /// <summary>
    /// Gets a value indicating whether authentication is configured.
    /// </summary>
    /// <value><c>true</c> if username or OAuth2 token is set; otherwise, <c>false</c>.</value>
    public bool RequiresAuthentication =>
        !string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(OAuth2AccessToken);
}
