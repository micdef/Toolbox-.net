// @file IMailingService.cs
// @brief Interface for email sending services
// @details Defines the contract for sending emails via SMTP
// @note Supports single and multiple recipients, attachments, and HTML/plain text content

using Toolbox.Core.Options;

namespace Toolbox.Core.Abstractions.Services;

/// <summary>
/// Defines the contract for email sending services.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides methods for sending emails with support for:
/// </para>
/// <list type="bullet">
///   <item><description>Single or multiple recipients (To, CC, BCC)</description></item>
///   <item><description>File attachments</description></item>
///   <item><description>HTML or plain text content</description></item>
/// </list>
/// </remarks>
/// <seealso cref="IInstrumentedService"/>
/// <seealso cref="IAsyncDisposableService"/>
public interface IMailingService : IInstrumentedService, IAsyncDisposableService
{
    /// <summary>
    /// Sends an email synchronously.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the message has no recipients (To or BCC).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the SMTP connection fails.</exception>
    /// <example>
    /// <code>
    /// var message = new EmailMessage
    /// {
    ///     From = new EmailAddress("sender@example.com", "Sender Name"),
    ///     Subject = "Hello",
    ///     Body = "Hello, World!",
    ///     To = { new EmailAddress("recipient@example.com") }
    /// };
    /// mailingService.SendMail(message);
    /// </code>
    /// </example>
    void SendMail(EmailMessage message);

    /// <summary>
    /// Sends an email asynchronously.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the message has no recipients (To or BCC).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the SMTP connection fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <example>
    /// <code>
    /// var message = new EmailMessage
    /// {
    ///     From = new EmailAddress("sender@example.com", "Sender Name"),
    ///     Subject = "Hello",
    ///     Body = "&lt;h1&gt;Hello, World!&lt;/h1&gt;",
    ///     IsBodyHtml = true,
    ///     To = { new EmailAddress("recipient@example.com") }
    /// };
    /// await mailingService.SendMailAsync(message);
    /// </code>
    /// </example>
    Task SendMailAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
