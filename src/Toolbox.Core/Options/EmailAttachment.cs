// @file EmailAttachment.cs
// @brief Email attachment representation
// @details Contains attachment data with filename and content type
// @note Supports both file paths and byte arrays

namespace Toolbox.Core.Options;

/// <summary>
/// Represents an email attachment.
/// </summary>
/// <remarks>
/// <para>
/// An attachment can be created from either a file path or raw byte data.
/// The content type is automatically detected from the filename if not specified.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // From file path
/// var attachment1 = EmailAttachment.FromFile("document.pdf");
///
/// // From byte array
/// var attachment2 = EmailAttachment.FromBytes(
///     pdfBytes,
///     "report.pdf",
///     "application/pdf");
/// </code>
/// </example>
public sealed class EmailAttachment
{
    /// <summary>
    /// Gets the filename of the attachment.
    /// </summary>
    /// <value>The filename to use in the email.</value>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the MIME content type of the attachment.
    /// </summary>
    /// <value>The content type (e.g., "application/pdf").</value>
    public required string ContentType { get; init; }

    /// <summary>
    /// Gets the attachment content as a byte array.
    /// </summary>
    /// <value>The raw content bytes, or <c>null</c> if using a file path.</value>
    public byte[]? Content { get; init; }

    /// <summary>
    /// Gets the file path of the attachment.
    /// </summary>
    /// <value>The file path, or <c>null</c> if using byte content.</value>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets a value indicating whether this attachment is inline (embedded in HTML).
    /// </summary>
    /// <value><c>true</c> if inline; otherwise, <c>false</c>.</value>
    public bool IsInline { get; init; }

    /// <summary>
    /// Gets the Content-ID for inline attachments.
    /// </summary>
    /// <value>The Content-ID for referencing in HTML, or <c>null</c>.</value>
    public string? ContentId { get; init; }

    /// <summary>
    /// Creates an attachment from a file path.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="contentType">The content type, or <c>null</c> to auto-detect.</param>
    /// <param name="fileName">The filename to use, or <c>null</c> to use the file's name.</param>
    /// <returns>An <see cref="EmailAttachment"/> representing the file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is <c>null</c>.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static EmailAttachment FromFile(
        string filePath,
        string? contentType = null,
        string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Attachment file not found.", filePath);
        }

        return new EmailAttachment
        {
            FilePath = filePath,
            FileName = fileName ?? Path.GetFileName(filePath),
            ContentType = contentType ?? GetContentType(filePath)
        };
    }

    /// <summary>
    /// Creates an attachment from a byte array.
    /// </summary>
    /// <param name="content">The attachment content.</param>
    /// <param name="fileName">The filename to use in the email.</param>
    /// <param name="contentType">The content type, or <c>null</c> to auto-detect from filename.</param>
    /// <returns>An <see cref="EmailAttachment"/> representing the content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> or <paramref name="fileName"/> is <c>null</c>.</exception>
    public static EmailAttachment FromBytes(
        byte[] content,
        string fileName,
        string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);

        return new EmailAttachment
        {
            Content = content,
            FileName = fileName,
            ContentType = contentType ?? GetContentType(fileName)
        };
    }

    /// <summary>
    /// Creates an inline attachment for embedding in HTML.
    /// </summary>
    /// <param name="content">The attachment content.</param>
    /// <param name="fileName">The filename.</param>
    /// <param name="contentId">The Content-ID for referencing in HTML (without angle brackets).</param>
    /// <param name="contentType">The content type, or <c>null</c> to auto-detect.</param>
    /// <returns>An inline <see cref="EmailAttachment"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// var logo = EmailAttachment.CreateInline(logoBytes, "logo.png", "company-logo");
    /// message.Body = "&lt;img src='cid:company-logo' /&gt;";
    /// message.Attachments.Add(logo);
    /// </code>
    /// </example>
    public static EmailAttachment CreateInline(
        byte[] content,
        string fileName,
        string contentId,
        string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(contentId);

        return new EmailAttachment
        {
            Content = content,
            FileName = fileName,
            ContentType = contentType ?? GetContentType(fileName),
            IsInline = true,
            ContentId = contentId
        };
    }

    /// <summary>
    /// Gets the content type based on file extension.
    /// </summary>
    /// <param name="fileName">The filename or path.</param>
    /// <returns>The MIME content type.</returns>
    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".7z" => "application/x-7z-compressed",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".html" or ".htm" => "text/html",
            ".xml" => "application/xml",
            ".json" => "application/json",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            _ => "application/octet-stream"
        };
    }
}
