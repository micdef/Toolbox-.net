// @file SmtpMailingService.cs
// @brief SMTP email sending service implementation
// @details Implements IMailingService using MailKit for secure SMTP
// @note Supports TLS/SSL, OAuth2, and various authentication methods

using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Mailing;

/// <summary>
/// Email sending service implementation using SMTP with MailKit.
/// </summary>
/// <remarks>
/// <para>
/// This service provides secure email sending via SMTP with support for:
/// </para>
/// <list type="bullet">
///   <item><description>TLS/SSL encryption (STARTTLS and implicit SSL)</description></item>
///   <item><description>Password and OAuth2 authentication</description></item>
///   <item><description>Anonymous connections for internal relays</description></item>
///   <item><description>Multiple recipients (To, CC, BCC)</description></item>
///   <item><description>HTML and plain text content</description></item>
///   <item><description>File attachments and inline images</description></item>
/// </list>
/// </remarks>
/// <seealso cref="IMailingService"/>
public sealed class SmtpMailingService : BaseAsyncDisposableService, IMailingService
{
    // The SMTP client instance
    private readonly SmtpClient _client;

    // The service options
    private readonly MailingOptions _options;

    // The logger instance
    private readonly ILogger<SmtpMailingService> _logger;

    // Lock for connection management
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    // Connection state
    private bool _isConnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpMailingService"/> class.
    /// </summary>
    /// <param name="options">The mailing service options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when host is empty.</exception>
    public SmtpMailingService(
        IOptions<MailingOptions> options,
        ILogger<SmtpMailingService> logger)
        : base("SmtpMailingService", logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new ArgumentException("SMTP host cannot be empty.", nameof(options));
        }

        _client = new SmtpClient
        {
            Timeout = (int)_options.ConnectionTimeout.TotalMilliseconds,
            ServerCertificateValidationCallback = _options.ValidateCertificate
                ? null
                : (_, _, _, _) => true
        };

        _logger.LogDebug(
            "SmtpMailingService initialized for {Host}:{Port} with security mode {SecurityMode}",
            _options.Host,
            _options.Port,
            _options.SecurityMode);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpMailingService"/> class.
    /// </summary>
    /// <param name="options">The mailing service options.</param>
    /// <param name="logger">The logger instance.</param>
    public SmtpMailingService(
        MailingOptions options,
        ILogger<SmtpMailingService> logger)
        : this(Microsoft.Extensions.Options.Options.Create(options), logger)
    {
    }

    /// <inheritdoc />
    public void SendMail(EmailMessage message)
    {
        SendMailAsync(message).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task SendMailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        message.Validate();

        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            var mimeMessage = BuildMimeMessage(message);

            await _client.SendAsync(mimeMessage, cancellationToken);

            _logger.LogDebug(
                "Email sent successfully to {RecipientCount} recipient(s): Subject='{Subject}'",
                message.TotalRecipients,
                message.Subject);

            RecordOperation("SendMail", sw.ElapsedMilliseconds);
            RecordEmailSent(message);
        }
        catch (SmtpCommandException ex)
        {
            _logger.LogError(
                ex,
                "SMTP command error while sending email: {StatusCode} - {Message}",
                ex.StatusCode,
                ex.Message);

            throw new InvalidOperationException($"SMTP error: {ex.Message}", ex);
        }
        catch (SmtpProtocolException ex)
        {
            _logger.LogError(ex, "SMTP protocol error while sending email");
            throw new InvalidOperationException($"SMTP protocol error: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not ArgumentNullException
                                    and not ArgumentException
                                    and not OperationCanceledException
                                    and not ObjectDisposedException
                                    and not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to send email");
            throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected && _client.IsConnected)
            {
                await _client.DisconnectAsync(true, cancellationToken);
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
    /// Builds a MimeMessage from an EmailMessage.
    /// </summary>
    /// <param name="message">The email message.</param>
    /// <returns>The constructed MimeMessage.</returns>
    private MimeMessage BuildMimeMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();

        // From address
        var from = message.From ?? _options.DefaultFrom
            ?? throw new ArgumentException("No sender address specified and no default configured.");

        mimeMessage.From.Add(new MailboxAddress(from.DisplayName, from.Address));

        // Reply-To
        var replyTo = message.ReplyTo ?? _options.DefaultReplyTo;
        if (replyTo is not null)
        {
            mimeMessage.ReplyTo.Add(new MailboxAddress(replyTo.DisplayName, replyTo.Address));
        }

        // Recipients
        foreach (var to in message.To)
        {
            mimeMessage.To.Add(new MailboxAddress(to.DisplayName, to.Address));
        }

        foreach (var cc in message.Cc)
        {
            mimeMessage.Cc.Add(new MailboxAddress(cc.DisplayName, cc.Address));
        }

        foreach (var bcc in message.Bcc)
        {
            mimeMessage.Bcc.Add(new MailboxAddress(bcc.DisplayName, bcc.Address));
        }

        // Subject
        mimeMessage.Subject = message.Subject;

        // Priority
        mimeMessage.Priority = message.Priority switch
        {
            Options.EmailPriority.Low => MessagePriority.NonUrgent,
            Options.EmailPriority.High => MessagePriority.Urgent,
            _ => MessagePriority.Normal
        };

        // Custom headers
        foreach (var (name, value) in message.Headers)
        {
            mimeMessage.Headers.Add(name, value);
        }

        // Body and attachments
        mimeMessage.Body = BuildMessageBody(message);

        return mimeMessage;
    }

    /// <summary>
    /// Builds the message body with proper MIME structure.
    /// </summary>
    /// <param name="message">The email message.</param>
    /// <returns>The message body as a MimeEntity.</returns>
    private static MimeEntity BuildMessageBody(EmailMessage message)
    {
        // Create the body part
        var textFormat = message.IsBodyHtml ? TextFormat.Html : TextFormat.Plain;
        var textPart = new TextPart(textFormat)
        {
            Text = message.Body
        };

        // If HTML with explicit plain text, create multipart/alternative
        MimeEntity bodyPart;
        if (message.IsBodyHtml && !string.IsNullOrEmpty(message.PlainTextBody))
        {
            var alternative = new Multipart("alternative")
            {
                new TextPart(TextFormat.Plain) { Text = message.PlainTextBody },
                textPart
            };
            bodyPart = alternative;
        }
        else
        {
            bodyPart = textPart;
        }

        // Handle attachments
        if (message.Attachments.Count == 0)
        {
            return bodyPart;
        }

        // Separate inline and regular attachments
        var inlineAttachments = message.Attachments.Where(a => a.IsInline).ToList();
        var regularAttachments = message.Attachments.Where(a => !a.IsInline).ToList();

        // If we have inline attachments and HTML body, wrap in multipart/related
        if (inlineAttachments.Count > 0 && message.IsBodyHtml)
        {
            var related = new Multipart("related") { bodyPart };

            foreach (var attachment in inlineAttachments)
            {
                related.Add(CreateMimePart(attachment));
            }

            bodyPart = related;
        }

        // If we have regular attachments, wrap in multipart/mixed
        if (regularAttachments.Count > 0)
        {
            var mixed = new Multipart("mixed") { bodyPart };

            foreach (var attachment in regularAttachments)
            {
                mixed.Add(CreateMimePart(attachment));
            }

            return mixed;
        }

        return bodyPart;
    }

    /// <summary>
    /// Creates a MimePart from an EmailAttachment.
    /// </summary>
    /// <param name="attachment">The email attachment.</param>
    /// <returns>The MimePart.</returns>
    private static MimePart CreateMimePart(EmailAttachment attachment)
    {
        var contentType = ContentType.Parse(attachment.ContentType);
        var mimePart = new MimePart(contentType)
        {
            FileName = attachment.FileName,
            ContentDisposition = new ContentDisposition(
                attachment.IsInline
                    ? ContentDisposition.Inline
                    : ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64
        };

        if (attachment.IsInline && !string.IsNullOrEmpty(attachment.ContentId))
        {
            mimePart.ContentId = attachment.ContentId;
        }

        // Set content
        if (attachment.Content is not null)
        {
            mimePart.Content = new MimeContent(new MemoryStream(attachment.Content));
        }
        else if (!string.IsNullOrEmpty(attachment.FilePath))
        {
            mimePart.Content = new MimeContent(File.OpenRead(attachment.FilePath));
        }

        return mimePart;
    }

    /// <summary>
    /// Ensures the client is connected to the SMTP server.
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
            if (_isConnected && _client.IsConnected)
            {
                return;
            }

            // Convert security mode to SecureSocketOptions
            var secureSocketOptions = _options.SecurityMode switch
            {
                SmtpSecurityMode.None => SecureSocketOptions.None,
                SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
                SmtpSecurityMode.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
                SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
                _ => SecureSocketOptions.Auto
            };

            await _client.ConnectAsync(
                _options.Host,
                _options.Port,
                secureSocketOptions,
                cancellationToken);

            _logger.LogDebug(
                "Connected to SMTP server {Host}:{Port}",
                _options.Host,
                _options.Port);

            // Authenticate if credentials are configured
            if (_options.RequiresAuthentication)
            {
                await AuthenticateAsync(cancellationToken);
            }

            _isConnected = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to SMTP server {Host}:{Port}", _options.Host, _options.Port);
            throw new InvalidOperationException($"Failed to connect to SMTP server: {ex.Message}", ex);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Authenticates with the SMTP server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrEmpty(_options.OAuth2AccessToken))
            {
                // OAuth2 authentication
                var oauth2 = new SaslMechanismOAuth2(_options.Username!, _options.OAuth2AccessToken);
                await _client.AuthenticateAsync(oauth2, cancellationToken);

                _logger.LogDebug("Authenticated with SMTP server using OAuth2");
            }
            else if (!string.IsNullOrEmpty(_options.Username))
            {
                // Password authentication
                await _client.AuthenticateAsync(
                    _options.Username,
                    _options.Password ?? string.Empty,
                    cancellationToken);

                _logger.LogDebug("Authenticated with SMTP server using password");
            }
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError(ex, "SMTP authentication failed");
            throw new InvalidOperationException($"SMTP authentication failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Records email metrics.
    /// </summary>
    /// <param name="message">The sent message.</param>
    private void RecordEmailSent(EmailMessage message)
    {
        var tags = new TagList
        {
            { TelemetryConstants.Attributes.ServiceName, ServiceName },
            { "toolbox.mailing.host", _options.Host },
            { "toolbox.mailing.recipients", message.TotalRecipients },
            { "toolbox.mailing.has_attachments", message.Attachments.Count > 0 },
            { "toolbox.mailing.is_html", message.IsBodyHtml }
        };

        ToolboxMeter.OperationCounter.Add(1, tags);
    }
}
