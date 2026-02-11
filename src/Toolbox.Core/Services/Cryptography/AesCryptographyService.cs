// @file AesCryptographyService.cs
// @brief AES encryption/decryption cryptography service
// @details Implements ICryptographyService using AES symmetric encryption
// @note Uses CBC mode with PKCS7 padding

using System.Security.Cryptography;
using System.Text;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Cryptography;

/// <summary>
/// Cryptography service implementation using AES symmetric encryption.
/// </summary>
/// <remarks>
/// <para>
/// This service provides secure text encryption and decryption using the AES algorithm.
/// It uses CBC mode with PKCS7 padding.
/// </para>
/// <para>
/// The encrypted output is Base64-encoded for safe text transmission.
/// </para>
/// </remarks>
/// <seealso cref="ICryptographyService"/>
public sealed class AesCryptographyService : BaseAsyncDisposableService, ICryptographyService
{
    /// <summary>
    /// The AES encryption key.
    /// </summary>
    private readonly byte[] _key;

    /// <summary>
    /// The initialization vector for cipher block chaining.
    /// </summary>
    private readonly byte[] _iv;

    /// <summary>
    /// The logger instance for diagnostic output.
    /// </summary>
    private readonly ILogger<AesCryptographyService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AesCryptographyService"/> class.
    /// </summary>
    /// <param name="key">The AES encryption key (16, 24, or 32 bytes for AES-128, AES-192, or AES-256).</param>
    /// <param name="iv">The initialization vector (must be 16 bytes).</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/>, <paramref name="iv"/>, or <paramref name="logger"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when key or IV size is invalid.</exception>
    public AesCryptographyService(
        byte[] key,
        byte[] iv,
        ILogger<AesCryptographyService> logger)
        : base("AesCryptographyService", logger)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(iv);

        ValidateKeySize(key.Length * 8);
        ValidateIvSize(iv.Length);

        _key = (byte[])key.Clone();
        _iv = (byte[])iv.Clone();
        _logger = logger;

        _logger.LogDebug(
            "AesCryptographyService initialized with {KeySize}-bit key",
            key.Length * 8);
    }

    /// <summary>
    /// Generates a new AES key and initialization vector.
    /// </summary>
    /// <param name="keySize">The key size (128, 192, or 256 bits).</param>
    /// <returns>A tuple containing the generated key and IV.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="keySize"/> is not 128, 192, or 256.</exception>
    /// <example>
    /// <code>
    /// var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
    /// var service = new AesCryptographyService(key, iv, logger);
    /// </code>
    /// </example>
    public static (byte[] Key, byte[] Iv) GenerateKey(AesKeySize keySize)
    {
        ValidateKeySize((int)keySize);

        using var aes = Aes.Create();
        aes.KeySize = (int)keySize;
        aes.GenerateKey();
        aes.GenerateIV();

        return (aes.Key, aes.IV);
    }

    /// <summary>
    /// Generates a new AES key and initialization vector.
    /// </summary>
    /// <param name="keySize">The key size in bits (128, 192, or 256).</param>
    /// <returns>A tuple containing the generated key and IV.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="keySize"/> is not 128, 192, or 256.</exception>
    public static (byte[] Key, byte[] Iv) GenerateKey(int keySize)
    {
        ValidateKeySize(keySize);

        using var aes = Aes.Create();
        aes.KeySize = keySize;
        aes.GenerateKey();
        aes.GenerateIV();

        return (aes.Key, aes.IV);
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
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            var result = Convert.ToBase64String(encryptedBytes);

            _logger.LogDebug(
                "Encrypted {InputLength} characters to {OutputLength} characters",
                plainText.Length,
                result.Length);

            RecordOperation("Encrypt", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordEncryption(ServiceName, "AES", plainBytes.Length, _key.Length * 8);
            return result;
        }
        catch (Exception ex) when (ex is not ArgumentNullException)
        {
            _logger.LogError(ex, "Failed to encrypt text");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(plainText);

        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var memoryStream = new MemoryStream();
            await using var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            await cryptoStream.WriteAsync(plainBytes, cancellationToken);
            await cryptoStream.FlushFinalBlockAsync(cancellationToken);

            var result = Convert.ToBase64String(memoryStream.ToArray());

            _logger.LogDebug(
                "Encrypted {InputLength} characters to {OutputLength} characters (async)",
                plainText.Length,
                result.Length);

            RecordOperation("EncryptAsync", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordEncryption(ServiceName, "AES", plainBytes.Length, _key.Length * 8);
            return result;
        }
        catch (Exception ex) when (ex is not ArgumentNullException and not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to encrypt text asynchronously");
            throw;
        }
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
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            var result = Encoding.UTF8.GetString(decryptedBytes);

            _logger.LogDebug(
                "Decrypted {InputLength} characters to {OutputLength} characters",
                encryptedText.Length,
                result.Length);

            RecordOperation("Decrypt", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordDecryption(ServiceName, "AES", decryptedBytes.Length, _key.Length * 8);
            return result;
        }
        catch (FormatException)
        {
            _logger.LogError("Failed to decrypt text: invalid Base64 format");
            throw;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt text: cryptographic error");
            throw new FormatException("The encrypted text is invalid or corrupted.", ex);
        }
        catch (Exception ex) when (ex is not ArgumentNullException and not FormatException)
        {
            _logger.LogError(ex, "Failed to decrypt text");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> DecryptAsync(string encryptedText, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(encryptedText);

        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var encryptedBytes = Convert.FromBase64String(encryptedText);

            using var inputStream = new MemoryStream(encryptedBytes);
            await using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
            using var outputStream = new MemoryStream();

            await cryptoStream.CopyToAsync(outputStream, cancellationToken);
            var result = Encoding.UTF8.GetString(outputStream.ToArray());

            _logger.LogDebug(
                "Decrypted {InputLength} characters to {OutputLength} characters (async)",
                encryptedText.Length,
                result.Length);

            RecordOperation("DecryptAsync", sw.ElapsedMilliseconds);
            ToolboxMeter.RecordDecryption(ServiceName, "AES", outputStream.ToArray().Length, _key.Length * 8);
            return result;
        }
        catch (FormatException)
        {
            _logger.LogError("Failed to decrypt text: invalid Base64 format");
            throw;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt text asynchronously: cryptographic error");
            throw new FormatException("The encrypted text is invalid or corrupted.", ex);
        }
        catch (Exception ex) when (ex is not ArgumentNullException and not FormatException and not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to decrypt text asynchronously");
            throw;
        }
    }

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        // Clear sensitive data from memory
        Array.Clear(_key, 0, _key.Length);
        Array.Clear(_iv, 0, _iv.Length);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Validates that the key size is valid for AES.
    /// </summary>
    /// <param name="keySizeInBits">The key size in bits.</param>
    /// <exception cref="ArgumentException">Thrown when the key size is invalid.</exception>
    private static void ValidateKeySize(int keySizeInBits)
    {
        if (keySizeInBits is not (128 or 192 or 256))
        {
            throw new ArgumentException(
                $"Invalid AES key size: {keySizeInBits} bits. Valid sizes are 128, 192, or 256 bits.",
                nameof(keySizeInBits));
        }
    }

    /// <summary>
    /// Validates that the IV size is valid for AES.
    /// </summary>
    /// <param name="ivSizeInBytes">The IV size in bytes.</param>
    /// <exception cref="ArgumentException">Thrown when the IV size is invalid.</exception>
    private static void ValidateIvSize(int ivSizeInBytes)
    {
        if (ivSizeInBytes != 16)
        {
            throw new ArgumentException(
                $"Invalid AES IV size: {ivSizeInBytes} bytes. IV must be exactly 16 bytes (128 bits).",
                nameof(ivSizeInBytes));
        }
    }
}
