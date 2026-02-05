using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Cryptography;

namespace Toolbox.Tests.Unit.Services.Cryptography;

/// <summary>
/// Unit tests for <see cref="Base64CryptographyService"/>.
/// </summary>
public class Base64CryptographyServiceTests
{
    private readonly Mock<ILogger<Base64CryptographyService>> _loggerMock;

    public Base64CryptographyServiceTests()
    {
        _loggerMock = new Mock<ILogger<Base64CryptographyService>>();
    }

    [Fact]
    public void Encrypt_WithStandardTable_ShouldEncodeCorrectly()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);

        // Act
        var result = service.Encrypt("Hello, World!");

        // Assert
        result.Should().Be("SGVsbG8sIFdvcmxkIQ==");
    }

    [Fact]
    public void Decrypt_WithStandardTable_ShouldDecodeCorrectly()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);

        // Act
        var result = service.Decrypt("SGVsbG8sIFdvcmxkIQ==");

        // Assert
        result.Should().Be("Hello, World!");
    }

    [Fact]
    public void Encrypt_ThenDecrypt_ShouldReturnOriginalText()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);
        var originalText = "The quick brown fox jumps over the lazy dog.";

        // Act
        var encrypted = service.Encrypt(originalText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(originalText);
    }

    [Fact]
    public void Encrypt_WithUrlSafeTable_ShouldUseUrlSafeCharacters()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.UrlSafe,
            _loggerMock.Object);

        // This text produces + and / in standard Base64: "subjects?_d"
        var text = "subjects?_d";

        // Act
        var result = service.Encrypt(text);

        // Assert
        result.Should().NotContain("+");
        result.Should().NotContain("/");
    }

    [Fact]
    public void Decrypt_WithUrlSafeTable_ShouldDecodeUrlSafeCharacters()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.UrlSafe,
            _loggerMock.Object);

        // Act
        var encrypted = service.Encrypt("test+data/here");
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be("test+data/here");
    }

    [Fact]
    public void Encrypt_WithNoPadding_ShouldOmitPaddingCharacters()
    {
        // Arrange
        var options = Options.Create(new Base64CryptographyOptions
        {
            EncodingTable = Base64EncodingTable.Standard,
            IncludePadding = false
        });
        using var service = new Base64CryptographyService(options, _loggerMock.Object);

        // Act
        var result = service.Encrypt("Hello, World!");

        // Assert
        result.Should().NotEndWith("=");
        result.Should().Be("SGVsbG8sIFdvcmxkIQ");
    }

    [Fact]
    public void Decrypt_WithoutPadding_ShouldStillDecodeCorrectly()
    {
        // Arrange
        var options = Options.Create(new Base64CryptographyOptions
        {
            EncodingTable = Base64EncodingTable.Standard,
            IncludePadding = false
        });
        using var service = new Base64CryptographyService(options, _loggerMock.Object);

        // Act - decode without padding
        var result = service.Decrypt("SGVsbG8sIFdvcmxkIQ");

        // Assert
        result.Should().Be("Hello, World!");
    }

    [Fact]
    public void Encrypt_WithNullText_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);

        // Act
        var act = () => service.Encrypt(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Decrypt_WithNullText_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);

        // Act
        var act = () => service.Decrypt(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ShouldThrowFormatException()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);

        // Act
        var act = () => service.Decrypt("Not@Valid#Base64!");

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public async Task EncryptAsync_ShouldEncodeCorrectly()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);

        // Act
        var result = await service.EncryptAsync("Hello, World!");

        // Assert
        result.Should().Be("SGVsbG8sIFdvcmxkIQ==");
    }

    [Fact]
    public async Task DecryptAsync_ShouldDecodeCorrectly()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);

        // Act
        var result = await service.DecryptAsync("SGVsbG8sIFdvcmxkIQ==");

        // Assert
        result.Should().Be("Hello, World!");
    }

    [Fact]
    public async Task EncryptAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);
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
        var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);
        service.Dispose();

        // Act
        var act = () => service.Encrypt("test");

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Encrypt_WithEmptyString_ShouldReturnEmptyBase64()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);

        // Act
        var result = service.Encrypt(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Encrypt_WithUnicodeText_ShouldHandleCorrectly()
    {
        // Arrange
        using var service = new Base64CryptographyService(
            Base64EncodingTable.Standard,
            _loggerMock.Object);
        var unicodeText = "Héllo, 世界! 🌍";

        // Act
        var encrypted = service.Encrypt(unicodeText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(unicodeText);
    }
}
