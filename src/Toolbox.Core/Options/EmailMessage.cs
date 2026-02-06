// @file EmailMessage.cs
// @brief Email message representation
// @details Contains all the data needed to send an email
// @note Supports multiple recipients, CC, BCC, and attachments

namespace Toolbox.Core.Options;

/// <summary>
/// Represents an email message to be sent.
/// </summary>
/// <remarks>
/// <para>
/// This class contains all the information needed to compose and send an email,
/// including recipients, subject, body, and attachments.
/// </para>
/// <para>
/// At least one recipient must be specified in <see cref="To"/> or <see cref="Bcc"/>.
/// If <see cref="To"/> is empty, at least one <see cref="Bcc"/> recipient is required.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var message = new EmailMessage
/// {
///     From = new EmailAddress("sender@example.com", "Sender"),
///     Subject = "Monthly Report",
///     Body = "&lt;h1&gt;Report&lt;/h1&gt;&lt;p&gt;See attached.&lt;/p&gt;",
///     IsBodyHtml = true,
///     To = { "recipient@example.com" },
///     Cc = { new EmailAddress("manager@example.com", "Manager") },
///     Attachments = { EmailAttachment.FromFile("report.pdf") }
/// };
/// </code>
/// </example>
public sealed class EmailMessage
{
    /// <summary>
    /// Gets or sets the sender's email address.
    /// </summary>
    /// <value>The sender address. If <c>null</c>, uses the service's default sender.</value>
    public EmailAddress? From { get; set; }

    /// <summary>
    /// Gets or sets the reply-to email address.
    /// </summary>
    /// <value>The reply-to address, or <c>null</c> to use the sender's address.</value>
    public EmailAddress? ReplyTo { get; set; }

    /// <summary>
    /// Gets the list of primary recipients.
    /// </summary>
    /// <value>The list of "To" recipients.</value>
    public IList<EmailAddress> To { get; } = new List<EmailAddress>();

    /// <summary>
    /// Gets the list of carbon copy recipients.
    /// </summary>
    /// <value>The list of "CC" recipients.</value>
    public IList<EmailAddress> Cc { get; } = new List<EmailAddress>();

    /// <summary>
    /// Gets the list of blind carbon copy recipients.
    /// </summary>
    /// <value>The list of "BCC" recipients.</value>
    public IList<EmailAddress> Bcc { get; } = new List<EmailAddress>();

    /// <summary>
    /// Gets or sets the email subject.
    /// </summary>
    /// <value>The subject line.</value>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email body content.
    /// </summary>
    /// <value>The body content in HTML or plain text format.</value>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the body is HTML.
    /// </summary>
    /// <value><c>true</c> if the body is HTML; <c>false</c> for plain text. Default is <c>false</c>.</value>
    public bool IsBodyHtml { get; set; }

    /// <summary>
    /// Gets or sets the plain text version of the body for HTML emails.
    /// </summary>
    /// <value>
    /// The plain text alternative, or <c>null</c> to auto-generate from HTML.
    /// Only used when <see cref="IsBodyHtml"/> is <c>true</c>.
    /// </value>
    public string? PlainTextBody { get; set; }

    /// <summary>
    /// Gets the list of attachments.
    /// </summary>
    /// <value>The list of file attachments.</value>
    public IList<EmailAttachment> Attachments { get; } = new List<EmailAttachment>();

    /// <summary>
    /// Gets or sets the email priority.
    /// </summary>
    /// <value>The message priority. Default is <see cref="EmailPriority.Normal"/>.</value>
    public EmailPriority Priority { get; set; } = EmailPriority.Normal;

    /// <summary>
    /// Gets the custom headers to include in the email.
    /// </summary>
    /// <value>A dictionary of custom header names and values.</value>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the total number of recipients (To + CC + BCC).
    /// </summary>
    /// <value>The total recipient count.</value>
    public int TotalRecipients => To.Count + Cc.Count + Bcc.Count;

    /// <summary>
    /// Gets a value indicating whether this message has any recipients.
    /// </summary>
    /// <value><c>true</c> if there is at least one recipient; otherwise, <c>false</c>.</value>
    public bool HasRecipients => To.Count > 0 || Bcc.Count > 0;

    /// <summary>
    /// Validates the message and throws if invalid.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the message is invalid.</exception>
    public void Validate()
    {
        if (!HasRecipients)
        {
            throw new ArgumentException("Email must have at least one recipient in To or BCC.");
        }
    }
}

/// <summary>
/// Specifies the priority of an email message.
/// </summary>
public enum EmailPriority
{
    /// <summary>Low priority.</summary>
    Low = 0,

    /// <summary>Normal priority (default).</summary>
    Normal = 1,

    /// <summary>High priority.</summary>
    High = 2
}
