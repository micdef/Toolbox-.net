using Microsoft.Extensions.Logging;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Cryptography;

namespace Toolbox.Tests.Unit.Services.Cryptography;

/// <summary>
/// Unit tests for <see cref="AesCryptographyService"/>.
/// </summary>
public class AesCryptographyServiceTests
{
    private readonly Mock<ILogger<AesCryptographyService>> _loggerMock;

    public AesCryptographyServiceTests()
    {
        _loggerMock = new Mock<ILogger<AesCryptographyService>>();
    }

    [Fact]
    public void GenerateKey_WithAes128_ShouldReturn16ByteKey()
    {
        // Act
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes128);

        // Assert
        key.Should().HaveCount(16);
        iv.Should().HaveCount(16);
    }

    [Fact]
    public void GenerateKey_WithAes192_ShouldReturn24ByteKey()
    {
        // Act
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes192);

        // Assert
        key.Should().HaveCount(24);
        iv.Should().HaveCount(16);
    }

    [Fact]
    public void GenerateKey_WithAes256_ShouldReturn32ByteKey()
    {
        // Act
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);

        // Assert
        key.Should().HaveCount(32);
        iv.Should().HaveCount(16);
    }

    [Fact]
    public void GenerateKey_WithIntKeySize_ShouldWork()
    {
        // Act
        var (key, iv) = AesCryptographyService.GenerateKey(256);

        // Assert
        key.Should().HaveCount(32);
        iv.Should().HaveCount(16);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(100)]
    [InlineData(512)]
    public void GenerateKey_WithInvalidKeySize_ShouldThrowArgumentException(int invalidKeySize)
    {
        // Act
        var act = () => AesCryptographyService.GenerateKey(invalidKeySize);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateKey_ShouldGenerateUniqueKeys()
    {
        // Act
        var (key1, iv1) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        var (key2, iv2) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);

        // Assert
        key1.Should().NotEqual(key2);
        iv1.Should().NotEqual(iv2);
    }

    [Fact]
    public void Constructor_WithNullKey_ShouldThrowArgumentNullException()
    {
        // Arrange
        var (_, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);

        // Act
        var act = () => new AesCryptographyService(null!, iv, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullIv_ShouldThrowArgumentNullException()
    {
        // Arrange
        var (key, _) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);

        // Act
        var act = () => new AesCryptographyService(key, null!, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithInvalidKeySize_ShouldThrowArgumentException()
    {
        // Arrange
        var invalidKey = new byte[10];
        var iv = new byte[16];

        // Act
        var act = () => new AesCryptographyService(invalidKey, iv, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithInvalidIvSize_ShouldThrowArgumentException()
    {
        // Arrange
        var (key, _) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        var invalidIv = new byte[8];

        // Act
        var act = () => new AesCryptographyService(key, invalidIv, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Encrypt_ShouldReturnBase64String()
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);

        // Act
        var result = service.Encrypt("Hello, World!");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var act = () => Convert.FromBase64String(result);
        act.Should().NotThrow();
    }

    [Fact]
    public void Encrypt_ThenDecrypt_ShouldReturnOriginalText()
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);
        var originalText = "The quick brown fox jumps over the lazy dog.";

        // Act
        var encrypted = service.Encrypt(originalText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(originalText);
    }

    [Theory]
    [InlineData(AesKeySize.Aes128)]
    [InlineData(AesKeySize.Aes192)]
    [InlineData(AesKeySize.Aes256)]
    public void Encrypt_ThenDecrypt_WithDifferentKeySizes_ShouldWork(AesKeySize keySize)
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(keySize);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);
        var originalText = "Test message for encryption.";

        // Act
        var encrypted = service.Encrypt(originalText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(originalText);
    }

    [Fact]
    public void Encrypt_WithSameInput_ShouldProduceSameOutput()
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);
        var text = "Test message";

        // Act
        var result1 = service.Encrypt(text);
        var result2 = service.Encrypt(text);

        // Assert - Same key/IV should produce same ciphertext (deterministic)
        result1.Should().Be(result2);
    }

    [Fact]
    public void Encrypt_WithDifferentKeys_ShouldProduceDifferentOutput()
    {
        // Arrange
        var (key1, iv1) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        var (key2, iv2) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service1 = new AesCryptographyService(key1, iv1, _loggerMock.Object);
        using var service2 = new AesCryptographyService(key2, iv2, _loggerMock.Object);
        var text = "Test message";

        // Act
        var result1 = service1.Encrypt(text);
        var result2 = service2.Encrypt(text);

        // Assert
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void Decrypt_WithWrongKey_ShouldThrowFormatException()
    {
        // Arrange
        var (key1, iv1) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        var (key2, iv2) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service1 = new AesCryptographyService(key1, iv1, _loggerMock.Object);
        using var service2 = new AesCryptographyService(key2, iv2, _loggerMock.Object);

        var encrypted = service1.Encrypt("Secret message");

        // Act
        var act = () => service2.Decrypt(encrypted);

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Encrypt_WithNullText_ShouldThrowArgumentNullException()
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);

        // Act
        var act = () => service.Encrypt(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Decrypt_WithNullText_ShouldThrowArgumentNullException()
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);

        // Act
        var act = () => service.Decrypt(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ShouldThrowFormatException()
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);

        // Act
        var act = () => service.Decrypt("Not@Valid#Base64!");

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public async Task EncryptAsync_ThenDecryptAsync_ShouldReturnOriginalText()
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);
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
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await service.EncryptAsync("test", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Encrypt_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        var service = new AesCryptographyService(key, iv, _loggerMock.Object);
        service.Dispose();

        // Act
        var act = () => service.Encrypt("test");

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Encrypt_WithEmptyString_ShouldWork()
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);

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
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);
        var unicodeText = "Héllo, 世界! 🌍 Привет мир!";

        // Act
        var encrypted = service.Encrypt(unicodeText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(unicodeText);
    }

    [Fact]
    public void Encrypt_WithLargeText_ShouldWork()
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        using var service = new AesCryptographyService(key, iv, _loggerMock.Object);
        var largeText = new string('A', 100000);

        // Act
        var encrypted = service.Encrypt(largeText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(largeText);
    }

    [Fact]
    public async Task DisposeAsync_ShouldClearSensitiveData()
    {
        // Arrange
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
        var service = new AesCryptographyService(key, iv, _loggerMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert - Service should be disposed
        var act = () => service.Encrypt("test");
        act.Should().Throw<ObjectDisposedException>();
    }
}
