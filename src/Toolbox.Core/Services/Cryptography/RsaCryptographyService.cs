// @file RsaCryptographyService.cs
// @brief RSA encryption/decryption cryptography service
// @details Implements ICryptographyService using RSA asymmetric encryption
// @note Supports multiple key sizes and padding modes

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;

namespace Toolbox.Core.Services.Cryptography;

/// <summary>
/// Cryptography service implementation using RSA asymmetric encryption.
/// </summary>
/// <remarks>
/// <para>
/// This service provides secure text encryption and decryption using the RSA algorithm.
/// It supports various key sizes and padding modes.
/// </para>
/// <para>
/// Encryption requires only the public key, while decryption requires the private key.
/// </para>
/// <para>
/// The encrypted output is Base64-encoded for safe text transmission.
/// </para>
/// </remarks>
/// <seealso cref="ICryptographyService"/>
public sealed class RsaCryptographyService : BaseAsyncDisposableService, ICryptographyService
{
    // The RSA instance for cryptographic operations
    private readonly RSA _rsa;

    // The padding mode to use
    private readonly RSAEncryptionPadding _padding;

    // Flag indicating if private key is available
    private readonly bool _hasPrivateKey;

    // The logger instance
    private readonly ILogger<RsaCryptographyService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaCryptographyService"/> class using raw key bytes.
    /// </summary>
    /// <param name="publicKey">The public key in SubjectPublicKeyInfo format.</param>
    /// <param name="paddingMode">The padding mode to use for encryption/decryption.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="privateKey">The private key in PKCS#8 format, or <c>null</c> if decryption is not needed.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="publicKey"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="CryptographicException">Thrown when the key format is invalid.</exception>
    /// <example>
    /// <code>
    /// var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
    /// using var service = new RsaCryptographyService(
    ///     keyPair.PublicKey!,
    ///     RsaPaddingMode.OaepSha256,
    ///     logger,
    ///     keyPair.PrivateKey);
    /// </code>
    /// </example>
    public RsaCryptographyService(
        byte[] publicKey,
        RsaPaddingMode paddingMode,
        ILogger<RsaCryptographyService> logger,
        byte[]? privateKey = null)
        : base("RsaCryptographyService", logger)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _padding = GetEncryptionPadding(paddingMode);
        _rsa = RSA.Create();

        try
        {
            _rsa.ImportSubjectPublicKeyInfo(publicKey, out _);

            if (privateKey is not null)
            {
                _rsa.ImportPkcs8PrivateKey(privateKey, out _);
                _hasPrivateKey = true;
            }
        }
        catch (Exception ex)
        {
            _rsa.Dispose();
            throw new CryptographicException("Failed to import RSA key(s).", ex);
        }

        _logger.LogDebug(
            "RsaCryptographyService initialized with {KeySize}-bit key, padding: {Padding}, has private key: {HasPrivateKey}",
            _rsa.KeySize,
            paddingMode,
            _hasPrivateKey);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaCryptographyService"/> class using an X.509 certificate.
    /// </summary>
    /// <param name="certificate">The X.509 certificate containing the RSA key(s).</param>
    /// <param name="paddingMode">The padding mode to use for encryption/decryption.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="certificate"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the certificate does not contain an RSA key.</exception>
    /// <example>
    /// <code>
    /// var cert = new X509Certificate2("certificate.pfx", "password");
    /// using var service = new RsaCryptographyService(cert, RsaPaddingMode.OaepSha256, logger);
    /// </code>
    /// </example>
    public RsaCryptographyService(
        X509Certificate2 certificate,
        RsaPaddingMode paddingMode,
        ILogger<RsaCryptographyService> logger)
        : base("RsaCryptographyService", logger)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _padding = GetEncryptionPadding(paddingMode);

        var publicRsa = certificate.GetRSAPublicKey()
            ?? throw new ArgumentException("Certificate does not contain an RSA public key.", nameof(certificate));

        _rsa = RSA.Create();

        try
        {
            // Import public key
            _rsa.ImportSubjectPublicKeyInfo(publicRsa.ExportSubjectPublicKeyInfo(), out _);

            // Import private key if available
            if (certificate.HasPrivateKey)
            {
                var privateRsa = certificate.GetRSAPrivateKey();
                if (privateRsa is not null)
                {
                    _rsa.ImportPkcs8PrivateKey(privateRsa.ExportPkcs8PrivateKey(), out _);
                    _hasPrivateKey = true;
                }
            }
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            _rsa.Dispose();
            throw new CryptographicException("Failed to import RSA key(s) from certificate.", ex);
        }

        _logger.LogDebug(
            "RsaCryptographyService initialized from certificate with {KeySize}-bit key, padding: {Padding}, has private key: {HasPrivateKey}",
            _rsa.KeySize,
            paddingMode,
            _hasPrivateKey);
    }

    /// <summary>
    /// Generates a new RSA key pair.
    /// </summary>
    /// <param name="keySize">The key size to generate.</param>
    /// <param name="includePrivateKey">Whether to include the private key in the result.</param>
    /// <returns>An <see cref="RsaKeyPair"/> containing the generated keys.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="keySize"/> is not a valid RSA key size.</exception>
    /// <example>
    /// <code>
    /// // Generate full key pair
    /// var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
    ///
    /// // Generate public key only (useful for sharing)
    /// var publicOnly = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048, includePrivateKey: false);
    /// </code>
    /// </example>
    public static RsaKeyPair GenerateKeyPair(RsaKeySize keySize, bool includePrivateKey = true)
    {
        ValidateKeySize((int)keySize);

        using var rsa = RSA.Create((int)keySize);

        var publicKey = rsa.ExportSubjectPublicKeyInfo();
        var privateKey = includePrivateKey ? rsa.ExportPkcs8PrivateKey() : null;

        return RsaKeyPair.FromBytes(publicKey, privateKey, (int)keySize);
    }

    /// <summary>
    /// Generates a new RSA key pair.
    /// </summary>
    /// <param name="keySize">The key size in bits (512, 1024, 2048, 4096, 8192, or 16384).</param>
    /// <param name="includePrivateKey">Whether to include the private key in the result.</param>
    /// <returns>An <see cref="RsaKeyPair"/> containing the generated keys.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="keySize"/> is not valid.</exception>
    public static RsaKeyPair GenerateKeyPair(int keySize, bool includePrivateKey = true)
    {
        ValidateKeySize(keySize);

        using var rsa = RSA.Create(keySize);

        var publicKey = rsa.ExportSubjectPublicKeyInfo();
        var privateKey = includePrivateKey ? rsa.ExportPkcs8PrivateKey() : null;

        return RsaKeyPair.FromBytes(publicKey, privateKey, keySize);
    }

    /// <summary>
    /// Generates a self-signed X.509 certificate containing an RSA key pair.
    /// </summary>
    /// <param name="keySize">The key size to generate.</param>
    /// <param name="subjectName">The certificate subject name (e.g., "CN=MyApp").</param>
    /// <param name="validityPeriod">The certificate validity period.</param>
    /// <param name="includePrivateKey">Whether to include the private key in the certificate.</param>
    /// <returns>An <see cref="RsaKeyPair"/> containing the generated certificate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="subjectName"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="keySize"/> is not valid.</exception>
    /// <example>
    /// <code>
    /// var keyPair = RsaCryptographyService.GenerateCertificate(
    ///     RsaKeySize.Rsa2048,
    ///     "CN=MyApplication",
    ///     TimeSpan.FromDays(365));
    /// </code>
    /// </example>
    public static RsaKeyPair GenerateCertificate(
        RsaKeySize keySize,
        string subjectName,
        TimeSpan validityPeriod,
        bool includePrivateKey = true)
    {
        ArgumentNullException.ThrowIfNull(subjectName);
        ValidateKeySize((int)keySize);

        using var rsa = RSA.Create((int)keySize);

        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.Add(validityPeriod);

        using var certWithPrivateKey = request.CreateSelfSigned(notBefore, notAfter);

        X509Certificate2 resultCert;
        if (includePrivateKey)
        {
            // Export and re-import to ensure private key is exportable
            var pfxBytes = certWithPrivateKey.Export(X509ContentType.Pfx);
            resultCert = X509CertificateLoader.LoadPkcs12(pfxBytes, null, X509KeyStorageFlags.Exportable);
        }
        else
        {
            // Export only the public certificate
            var certBytes = certWithPrivateKey.Export(X509ContentType.Cert);
            resultCert = X509CertificateLoader.LoadCertificate(certBytes);
        }

        return RsaKeyPair.FromCertificate(resultCert);
    }

    /// <summary>
    /// Generates a self-signed X.509 certificate containing an RSA key pair.
    /// </summary>
    /// <param name="keySize">The key size in bits.</param>
    /// <param name="subjectName">The certificate subject name.</param>
    /// <param name="validityPeriod">The certificate validity period.</param>
    /// <param name="includePrivateKey">Whether to include the private key in the certificate.</param>
    /// <returns>An <see cref="RsaKeyPair"/> containing the generated certificate.</returns>
    public static RsaKeyPair GenerateCertificate(
        int keySize,
        string subjectName,
        TimeSpan validityPeriod,
        bool includePrivateKey = true)
    {
        ValidateKeySize(keySize);
        return GenerateCertificate((RsaKeySize)keySize, subjectName, validityPeriod, includePrivateKey);
    }

    /// <inheritdoc />
    /// <exception cref="CryptographicException">Thrown when encryption fails.</exception>
    /// <remarks>
    /// RSA can only encrypt data smaller than the key size minus padding overhead.
    /// For larger data, consider using hybrid encryption (RSA + AES).
    /// </remarks>
    public string Encrypt(string plainText)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(plainText);

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var maxDataLength = GetMaxDataLength();

            if (plainBytes.Length > maxDataLength)
            {
                throw new CryptographicException(
                    $"Data too large for RSA encryption. Maximum size is {maxDataLength} bytes, but data is {plainBytes.Length} bytes. " +
                    "Consider using hybrid encryption (RSA + AES) for larger data.");
            }

            var encryptedBytes = _rsa.Encrypt(plainBytes, _padding);
            var result = Convert.ToBase64String(encryptedBytes);

            _logger.LogDebug(
                "Encrypted {InputLength} bytes to {OutputLength} characters",
                plainBytes.Length,
                result.Length);

            RecordOperation("Encrypt", sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex) when (ex is not ArgumentNullException and not CryptographicException)
        {
            _logger.LogError(ex, "Failed to encrypt text");
            throw new CryptographicException("RSA encryption failed.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(plainText);

        cancellationToken.ThrowIfCancellationRequested();

        // RSA encryption is CPU-bound, use Task.Run for async
        return await Task.Run(() => Encrypt(plainText), cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when private key is not available.</exception>
    /// <exception cref="FormatException">Thrown when decryption fails due to invalid data.</exception>
    public string Decrypt(string encryptedText)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(encryptedText);

        if (!_hasPrivateKey)
        {
            throw new InvalidOperationException(
                "Cannot decrypt without a private key. Ensure the service was initialized with a private key.");
        }

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var decryptedBytes = _rsa.Decrypt(encryptedBytes, _padding);
            var result = Encoding.UTF8.GetString(decryptedBytes);

            _logger.LogDebug(
                "Decrypted {InputLength} characters to {OutputLength} bytes",
                encryptedText.Length,
                decryptedBytes.Length);

            RecordOperation("Decrypt", sw.ElapsedMilliseconds);
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
        catch (Exception ex) when (ex is not ArgumentNullException and not FormatException and not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to decrypt text");
            throw new FormatException("Decryption failed.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<string> DecryptAsync(string encryptedText, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(encryptedText);

        cancellationToken.ThrowIfCancellationRequested();

        // RSA decryption is CPU-bound, use Task.Run for async
        return await Task.Run(() => Decrypt(encryptedText), cancellationToken);
    }

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        _rsa.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Converts the <see cref="RsaPaddingMode"/> enum to <see cref="RSAEncryptionPadding"/>.
    /// </summary>
    /// <param name="mode">The padding mode enum value.</param>
    /// <returns>The corresponding <see cref="RSAEncryptionPadding"/> instance.</returns>
    private static RSAEncryptionPadding GetEncryptionPadding(RsaPaddingMode mode) => mode switch
    {
        RsaPaddingMode.Pkcs1 => RSAEncryptionPadding.Pkcs1,
        RsaPaddingMode.OaepSha1 => RSAEncryptionPadding.OaepSHA1,
        RsaPaddingMode.OaepSha256 => RSAEncryptionPadding.OaepSHA256,
        RsaPaddingMode.OaepSha384 => RSAEncryptionPadding.OaepSHA384,
        RsaPaddingMode.OaepSha512 => RSAEncryptionPadding.OaepSHA512,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid padding mode.")
    };

    /// <summary>
    /// Validates that the key size is valid for RSA.
    /// </summary>
    /// <param name="keySizeInBits">The key size in bits.</param>
    /// <exception cref="ArgumentException">Thrown when the key size is invalid.</exception>
    private static void ValidateKeySize(int keySizeInBits)
    {
        if (keySizeInBits is not (512 or 1024 or 2048 or 4096 or 8192 or 16384))
        {
            throw new ArgumentException(
                $"Invalid RSA key size: {keySizeInBits} bits. Valid sizes are 512, 1024, 2048, 4096, 8192, or 16384 bits.",
                nameof(keySizeInBits));
        }
    }

    /// <summary>
    /// Gets the maximum data length that can be encrypted with the current key and padding.
    /// </summary>
    /// <returns>The maximum number of bytes that can be encrypted.</returns>
    private int GetMaxDataLength()
    {
        var keyBytes = _rsa.KeySize / 8;

        // OAEP overhead depends on hash size
        // Formula: keyBytes - 2 * hashBytes - 2
        return _padding.OaepHashAlgorithm.Name switch
        {
            "SHA1" => keyBytes - 42,    // 2 * 20 + 2 = 42
            "SHA256" => keyBytes - 66,  // 2 * 32 + 2 = 66
            "SHA384" => keyBytes - 98,  // 2 * 48 + 2 = 98
            "SHA512" => keyBytes - 130, // 2 * 64 + 2 = 130
            _ => keyBytes - 11          // PKCS#1 v1.5 overhead is 11 bytes
        };
    }
}
