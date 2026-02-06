using Microsoft.Extensions.Logging;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Cryptography;

namespace Toolbox.Tests.Unit.Services.Cryptography;

/// <summary>
/// Unit tests for <see cref="RsaCryptographyService"/>.
/// </summary>
public class RsaCryptographyServiceTests
{
    private readonly Mock<ILogger<RsaCryptographyService>> _loggerMock;

    public RsaCryptographyServiceTests()
    {
        _loggerMock = new Mock<ILogger<RsaCryptographyService>>();
    }

    #region GenerateKeyPair Tests

    [Theory]
    [InlineData(RsaKeySize.Rsa512)]
    [InlineData(RsaKeySize.Rsa1024)]
    [InlineData(RsaKeySize.Rsa2048)]
    public void GenerateKeyPair_WithValidKeySize_ShouldReturnKeyPair(RsaKeySize keySize)
    {
        // Act
        using var keyPair = RsaCryptographyService.GenerateKeyPair(keySize);

        // Assert
        keyPair.Should().NotBeNull();
        keyPair.PublicKey.Should().NotBeNull();
        keyPair.PrivateKey.Should().NotBeNull();
        keyPair.HasPrivateKey.Should().BeTrue();
        keyPair.KeySize.Should().Be((int)keySize);
    }

    [Fact]
    public void GenerateKeyPair_WithIntKeySize_ShouldWork()
    {
        // Act
        using var keyPair = RsaCryptographyService.GenerateKeyPair(2048);

        // Assert
        keyPair.PublicKey.Should().NotBeNull();
        keyPair.PrivateKey.Should().NotBeNull();
        keyPair.KeySize.Should().Be(2048);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(3072)]
    [InlineData(5000)]
    public void GenerateKeyPair_WithInvalidKeySize_ShouldThrowArgumentException(int invalidKeySize)
    {
        // Act
        var act = () => RsaCryptographyService.GenerateKeyPair(invalidKeySize);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateKeyPair_WithoutPrivateKey_ShouldReturnPublicOnly()
    {
        // Act
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048, includePrivateKey: false);

        // Assert
        keyPair.PublicKey.Should().NotBeNull();
        keyPair.PrivateKey.Should().BeNull();
        keyPair.HasPrivateKey.Should().BeFalse();
    }

    [Fact]
    public void GenerateKeyPair_ShouldGenerateUniqueKeys()
    {
        // Act
        using var keyPair1 = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var keyPair2 = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);

        // Assert
        keyPair1.PublicKey.Should().NotEqual(keyPair2.PublicKey);
        keyPair1.PrivateKey.Should().NotEqual(keyPair2.PrivateKey);
    }

    #endregion

    #region GenerateCertificate Tests

    [Fact]
    public void GenerateCertificate_ShouldReturnValidCertificate()
    {
        // Act
        using var keyPair = RsaCryptographyService.GenerateCertificate(
            RsaKeySize.Rsa2048,
            "CN=TestCertificate",
            TimeSpan.FromDays(365));

        // Assert
        keyPair.Should().NotBeNull();
        keyPair.Certificate.Should().NotBeNull();
        keyPair.Certificate!.Subject.Should().Contain("TestCertificate");
        keyPair.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void GenerateCertificate_WithoutPrivateKey_ShouldReturnPublicCertOnly()
    {
        // Act
        using var keyPair = RsaCryptographyService.GenerateCertificate(
            RsaKeySize.Rsa2048,
            "CN=TestCertificate",
            TimeSpan.FromDays(365),
            includePrivateKey: false);

        // Assert
        keyPair.Certificate.Should().NotBeNull();
        keyPair.Certificate!.HasPrivateKey.Should().BeFalse();
    }

    [Fact]
    public void GenerateCertificate_WithNullSubjectName_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => RsaCryptographyService.GenerateCertificate(
            RsaKeySize.Rsa2048,
            null!,
            TimeSpan.FromDays(365));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullPublicKey_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new RsaCryptographyService(
            null!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);

        // Act
        var act = () => new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullCertificate_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new RsaCryptographyService(
            (System.Security.Cryptography.X509Certificates.X509Certificate2)null!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidKeyPair_ShouldInitialize()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);

        // Act
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair.PrivateKey);

        // Assert - No exception means success
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCertificate_ShouldInitialize()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateCertificate(
            RsaKeySize.Rsa2048,
            "CN=Test",
            TimeSpan.FromDays(1));

        // Act
        using var service = new RsaCryptographyService(
            keyPair.Certificate!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region Encrypt Tests

    [Fact]
    public void Encrypt_ShouldReturnBase64String()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair.PrivateKey);

        // Act
        var result = service.Encrypt("Hello, World!");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var act = () => Convert.FromBase64String(result);
        act.Should().NotThrow();
    }

    [Fact]
    public void Encrypt_WithNullText_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);

        // Act
        var act = () => service.Encrypt(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Encrypt_WithPublicKeyOnly_ShouldWork()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);  // No private key

        // Act
        var result = service.Encrypt("Test message");

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Encrypt_WithTooLargeData_ShouldThrowCryptographicException()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);

        // RSA-2048 with OAEP-SHA256 can encrypt max 190 bytes
        var largeText = new string('A', 500);

        // Act
        var act = () => service.Encrypt(largeText);

        // Assert
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void Encrypt_WithDifferentKeys_ShouldProduceDifferentOutput()
    {
        // Arrange
        using var keyPair1 = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var keyPair2 = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service1 = new RsaCryptographyService(
            keyPair1.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);
        using var service2 = new RsaCryptographyService(
            keyPair2.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);
        var text = "Test message";

        // Act
        var result1 = service1.Encrypt(text);
        var result2 = service2.Encrypt(text);

        // Assert
        result1.Should().NotBe(result2);
    }

    #endregion

    #region Decrypt Tests

    [Fact]
    public void Encrypt_ThenDecrypt_ShouldReturnOriginalText()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair.PrivateKey);
        var originalText = "The quick brown fox jumps over the lazy dog.";

        // Act
        var encrypted = service.Encrypt(originalText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(originalText);
    }

    [Theory]
    [InlineData(RsaPaddingMode.Pkcs1)]
    [InlineData(RsaPaddingMode.OaepSha1)]
    [InlineData(RsaPaddingMode.OaepSha256)]
    [InlineData(RsaPaddingMode.OaepSha384)]
    [InlineData(RsaPaddingMode.OaepSha512)]
    public void Encrypt_ThenDecrypt_WithDifferentPaddingModes_ShouldWork(RsaPaddingMode paddingMode)
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            paddingMode,
            _loggerMock.Object,
            keyPair.PrivateKey);
        var originalText = "Test message";

        // Act
        var encrypted = service.Encrypt(originalText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(originalText);
    }

    [Theory]
    [InlineData(RsaKeySize.Rsa512)]
    [InlineData(RsaKeySize.Rsa1024)]
    [InlineData(RsaKeySize.Rsa2048)]
    public void Encrypt_ThenDecrypt_WithDifferentKeySizes_ShouldWork(RsaKeySize keySize)
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(keySize);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.Pkcs1,  // Use PKCS1 for smaller keys
            _loggerMock.Object,
            keyPair.PrivateKey);
        var originalText = "Short test";

        // Act
        var encrypted = service.Encrypt(originalText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(originalText);
    }

    [Fact]
    public void Decrypt_WithoutPrivateKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var encryptService = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair.PrivateKey);
        using var decryptService = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);  // No private key

        var encrypted = encryptService.Encrypt("Test");

        // Act
        var act = () => decryptService.Decrypt(encrypted);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*private key*");
    }

    [Fact]
    public void Decrypt_WithNullText_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair.PrivateKey);

        // Act
        var act = () => service.Decrypt(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ShouldThrowFormatException()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair.PrivateKey);

        // Act
        var act = () => service.Decrypt("Not@Valid#Base64!");

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Decrypt_WithWrongKey_ShouldThrowFormatException()
    {
        // Arrange
        using var keyPair1 = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var keyPair2 = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service1 = new RsaCryptographyService(
            keyPair1.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair1.PrivateKey);
        using var service2 = new RsaCryptographyService(
            keyPair2.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair2.PrivateKey);

        var encrypted = service1.Encrypt("Secret message");

        // Act
        var act = () => service2.Decrypt(encrypted);

        // Assert
        act.Should().Throw<FormatException>();
    }

    #endregion

    #region Async Tests

    [Fact]
    public async Task EncryptAsync_ThenDecryptAsync_ShouldReturnOriginalText()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair.PrivateKey);
        var originalText = "Async encryption test message.";

        // Act
        var encrypted = await service.EncryptAsync(originalText);
        var decrypted = await service.DecryptAsync(encrypted);

        // Assert
        decrypted.Should().Be(originalText);
    }

    [Fact]
    public async Task EncryptAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair.PrivateKey);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await service.EncryptAsync("test", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DecryptAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair.PrivateKey);
        using var cts = new CancellationTokenSource();
        var encrypted = service.Encrypt("test");
        await cts.CancelAsync();

        // Act
        var act = async () => await service.DecryptAsync(encrypted, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Encrypt_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);
        service.Dispose();

        // Act
        var act = () => service.Encrypt("test");

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeResources()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert - Service should be disposed
        var act = () => service.Encrypt("test");
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Unicode and Edge Cases

    [Fact]
    public void Encrypt_WithEmptyString_ShouldWork()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair.PrivateKey);

        // Act
        var encrypted = service.Encrypt(string.Empty);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().BeEmpty();
    }

    [Fact]
    public void Encrypt_WithUnicodeText_ShouldHandleCorrectly()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        using var service = new RsaCryptographyService(
            keyPair.PublicKey!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object,
            keyPair.PrivateKey);
        var unicodeText = "Héllo, 世界!";

        // Act
        var encrypted = service.Encrypt(unicodeText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(unicodeText);
    }

    #endregion

    #region Certificate Tests

    [Fact]
    public void Encrypt_ThenDecrypt_WithCertificate_ShouldWork()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateCertificate(
            RsaKeySize.Rsa2048,
            "CN=TestCert",
            TimeSpan.FromDays(1));
        using var service = new RsaCryptographyService(
            keyPair.Certificate!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);
        var originalText = "Certificate encryption test.";

        // Act
        var encrypted = service.Encrypt(originalText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(originalText);
    }

    [Fact]
    public void Decrypt_WithPublicOnlyCertificate_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var fullKeyPair = RsaCryptographyService.GenerateCertificate(
            RsaKeySize.Rsa2048,
            "CN=TestCert",
            TimeSpan.FromDays(1));
        using var publicOnlyKeyPair = fullKeyPair.ToPublicOnly();

        using var encryptService = new RsaCryptographyService(
            fullKeyPair.Certificate!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);
        using var decryptService = new RsaCryptographyService(
            publicOnlyKeyPair.Certificate!,
            RsaPaddingMode.OaepSha256,
            _loggerMock.Object);

        var encrypted = encryptService.Encrypt("Test");

        // Act
        var act = () => decryptService.Decrypt(encrypted);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region RsaKeyPair Tests

    [Fact]
    public void RsaKeyPair_ToPublicOnly_ShouldRemovePrivateKey()
    {
        // Arrange
        using var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);

        // Act
        using var publicOnly = keyPair.ToPublicOnly();

        // Assert
        publicOnly.PublicKey.Should().NotBeNull();
        publicOnly.PrivateKey.Should().BeNull();
        publicOnly.HasPrivateKey.Should().BeFalse();
    }

    [Fact]
    public void RsaKeyPair_Dispose_ShouldClearSensitiveData()
    {
        // Arrange
        var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
        var privateKeyCopy = (byte[])keyPair.PrivateKey!.Clone();

        // Act
        keyPair.Dispose();

        // Assert - Private key should be cleared (all zeros)
        keyPair.PrivateKey!.All(b => b == 0).Should().BeTrue();
    }

    #endregion
}
