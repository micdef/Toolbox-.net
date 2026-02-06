// @file Base64CryptographyService.cs
// @brief Base64 encoding/decoding cryptography service
// @details Implements ICryptographyService using Base64 encoding
// @note Supports both standard and URL-safe encoding tables

using System.Buffers.Text;
using System.Text;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Cryptography;

/// <summary>
/// Cryptography service implementation using Base64 encoding.
/// </summary>
/// <remarks>
/// <para>
/// This service provides text encryption and decryption using Base64 encoding.
/// It supports both standard and URL-safe encoding tables.
/// </para>
/// <para>
/// Note: Base64 is an encoding scheme, not encryption. It provides obfuscation
/// but not security. For secure encryption, use a proper encryption algorithm.
/// </para>
/// </remarks>
/// <seealso cref="ICryptographyService"/>
/// <seealso cref="Base64CryptographyOptions"/>
public sealed class Base64CryptographyService : BaseAsyncDisposableService, ICryptographyService
{
    // The encoding table configuration
    private readonly Base64EncodingTable _encodingTable;

    // Whether to include padding in output
    private readonly bool _includePadding;

    // The logger instance
    private readonly ILogger<Base64CryptographyService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Base64CryptographyService"/> class.
    /// </summary>
    /// <param name="encodingTable">The Base64 encoding table to use.</param>
    /// <param name="logger">The logger instance.</param>
    public Base64CryptographyService(
        Base64EncodingTable encodingTable,
        ILogger<Base64CryptographyService> logger)
        : base("Base64CryptographyService", logger)
    {
        _encodingTable = encodingTable;
        _includePadding = true;
        _logger = logger;

        _logger.LogDebug(
            "Base64CryptographyService initialized with encoding table: {EncodingTable}",
            encodingTable);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Base64CryptographyService"/> class
    /// with full options.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public Base64CryptographyService(
        IOptions<Base64CryptographyOptions> options,
        ILogger<Base64CryptographyService> logger)
        : base("Base64CryptographyService", logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        var opts = options.Value;
        _encodingTable = opts.EncodingTable;
        _includePadding = opts.IncludePadding;
        _logger = logger;

        _logger.LogDebug(
            "Base64CryptographyService initialized with encoding table: {EncodingTable}, padding: {IncludePadding}",
            _encodingTable,
            _includePadding);
    }

    /// <inheritdoc />
    public string Encrypt(string plainText)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(plainText);

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var result = EncodeToBase64(bytes);

            _logger.LogDebug(
                "Encrypted {InputLength} characters to {OutputLength} characters",
                plainText.Length,
                result.Length);

            RecordOperation("Encrypt", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordEncryption(ServiceName, "Base64", bytes.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt text");
            throw;
        }
    }

    /// <inheritdoc />
    public Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(plainText);

        cancellationToken.ThrowIfCancellationRequested();

        // Base64 encoding is CPU-bound and fast, so we run it synchronously
        // but wrap in Task.FromResult for the async interface
        return Task.FromResult(Encrypt(plainText));
    }

    /// <inheritdoc />
    public string Decrypt(string encryptedText)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(encryptedText);

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var bytes = DecodeFromBase64(encryptedText);
            var result = Encoding.UTF8.GetString(bytes);

            _logger.LogDebug(
                "Decrypted {InputLength} characters to {OutputLength} characters",
                encryptedText.Length,
                result.Length);

            RecordOperation("Decrypt", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordDecryption(ServiceName, "Base64", bytes.Length);
            return result;
        }
        catch (FormatException)
        {
            _logger.LogError("Failed to decrypt text: invalid Base64 format");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt text");
            throw;
        }
    }

    /// <inheritdoc />
    public Task<string> DecryptAsync(string encryptedText, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(encryptedText);

        cancellationToken.ThrowIfCancellationRequested();

        // Base64 decoding is CPU-bound and fast, so we run it synchronously
        // but wrap in Task.FromResult for the async interface
        return Task.FromResult(Decrypt(encryptedText));
    }

    /// <summary>
    /// Encodes bytes to Base64 string using the configured encoding table.
    /// </summary>
    /// <param name="bytes">The bytes to encode.</param>
    /// <returns>The Base64 encoded string.</returns>
    private string EncodeToBase64(byte[] bytes)
    {
        var base64 = Convert.ToBase64String(bytes);

        // Convert to URL-safe if needed
        if (_encodingTable == Base64EncodingTable.UrlSafe)
        {
            base64 = base64.Replace('+', '-').Replace('/', '_');
        }

        // Remove padding if configured
        if (!_includePadding)
        {
            base64 = base64.TrimEnd('=');
        }

        return base64;
    }

    /// <summary>
    /// Decodes Base64 string to bytes using the configured encoding table.
    /// </summary>
    /// <param name="base64">The Base64 string to decode.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="FormatException">Thrown when the input is not valid Base64.</exception>
    private byte[] DecodeFromBase64(string base64)
    {
        // Convert from URL-safe if needed
        if (_encodingTable == Base64EncodingTable.UrlSafe)
        {
            base64 = base64.Replace('-', '+').Replace('_', '/');
        }

        // Add padding if missing
        var paddingNeeded = base64.Length % 4;
        if (paddingNeeded > 0)
        {
            base64 += new string('=', 4 - paddingNeeded);
        }

        return Convert.FromBase64String(base64);
    }
}
