// @file RsaKeyPair.cs
// @brief Container for RSA key pair data
// @details Holds public and optional private key in various formats
// @note Use the static factory methods to create instances

using System.Security.Cryptography.X509Certificates;

namespace Toolbox.Core.Options;

/// <summary>
/// Represents an RSA key pair containing public and optionally private key data.
/// </summary>
/// <remarks>
/// <para>
/// This class provides multiple formats for RSA keys:
/// raw byte arrays and X.509 certificates.
/// </para>
/// <para>
/// The private key is optional - some operations (like encryption) only require the public key.
/// </para>
/// </remarks>
public sealed class RsaKeyPair : IDisposable
{
    // Flag indicating if the object has been disposed
    private bool _disposed;

    /// <summary>
    /// Gets the public key in DER-encoded SubjectPublicKeyInfo format.
    /// </summary>
    /// <value>The public key bytes, or <c>null</c> if not available in this format.</value>
    public byte[]? PublicKey { get; private init; }

    /// <summary>
    /// Gets the private key in DER-encoded PKCS#8 format.
    /// </summary>
    /// <value>The private key bytes, or <c>null</c> if not available or not included.</value>
    public byte[]? PrivateKey { get; private init; }

    /// <summary>
    /// Gets the X.509 certificate containing the public key.
    /// </summary>
    /// <value>The certificate, or <c>null</c> if not available in this format.</value>
    public X509Certificate2? Certificate { get; private init; }

    /// <summary>
    /// Gets a value indicating whether this key pair contains a private key.
    /// </summary>
    /// <value><c>true</c> if a private key is available; otherwise, <c>false</c>.</value>
    public bool HasPrivateKey => PrivateKey is not null || Certificate?.HasPrivateKey == true;

    /// <summary>
    /// Gets the key size in bits.
    /// </summary>
    /// <value>The RSA key size.</value>
    public int KeySize { get; private init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaKeyPair"/> class.
    /// </summary>
    /// <remarks>
    /// Use the static factory methods to create instances.
    /// </remarks>
    private RsaKeyPair()
    {
    }

    /// <summary>
    /// Creates an <see cref="RsaKeyPair"/> from raw byte arrays.
    /// </summary>
    /// <param name="publicKey">The public key in SubjectPublicKeyInfo format.</param>
    /// <param name="privateKey">The private key in PKCS#8 format, or <c>null</c> for public-only.</param>
    /// <param name="keySize">The key size in bits.</param>
    /// <returns>A new <see cref="RsaKeyPair"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="publicKey"/> is <c>null</c>.</exception>
    public static RsaKeyPair FromBytes(byte[] publicKey, byte[]? privateKey, int keySize)
    {
        ArgumentNullException.ThrowIfNull(publicKey);

        return new RsaKeyPair
        {
            PublicKey = (byte[])publicKey.Clone(),
            PrivateKey = privateKey is not null ? (byte[])privateKey.Clone() : null,
            KeySize = keySize
        };
    }

    /// <summary>
    /// Creates an <see cref="RsaKeyPair"/> from an X.509 certificate.
    /// </summary>
    /// <param name="certificate">The certificate containing the RSA public key.</param>
    /// <returns>A new <see cref="RsaKeyPair"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="certificate"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the certificate does not contain an RSA key.</exception>
    public static RsaKeyPair FromCertificate(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var rsa = certificate.GetRSAPublicKey()
            ?? throw new ArgumentException("Certificate does not contain an RSA public key.", nameof(certificate));

        return new RsaKeyPair
        {
            Certificate = certificate,
            PublicKey = rsa.ExportSubjectPublicKeyInfo(),
            PrivateKey = certificate.HasPrivateKey ? certificate.GetRSAPrivateKey()?.ExportPkcs8PrivateKey() : null,
            KeySize = rsa.KeySize
        };
    }

    /// <summary>
    /// Creates an <see cref="RsaKeyPair"/> containing only the public key from this pair.
    /// </summary>
    /// <returns>A new <see cref="RsaKeyPair"/> with only the public key.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    public RsaKeyPair ToPublicOnly()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Certificate is not null)
        {
            // Create certificate without private key
            var publicCert = X509CertificateLoader.LoadCertificate(Certificate.RawData);
            return new RsaKeyPair
            {
                Certificate = publicCert,
                PublicKey = PublicKey is not null ? (byte[])PublicKey.Clone() : null,
                PrivateKey = null,
                KeySize = KeySize
            };
        }

        return new RsaKeyPair
        {
            PublicKey = PublicKey is not null ? (byte[])PublicKey.Clone() : null,
            PrivateKey = null,
            KeySize = KeySize
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Clear sensitive private key data
        if (PrivateKey is not null)
        {
            Array.Clear(PrivateKey, 0, PrivateKey.Length);
        }

        Certificate?.Dispose();

        _disposed = true;
    }
}
