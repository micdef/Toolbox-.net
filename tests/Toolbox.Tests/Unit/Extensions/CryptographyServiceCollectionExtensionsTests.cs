using Microsoft.Extensions.DependencyInjection;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Cryptography;

namespace Toolbox.Tests.Unit.Extensions;

/// <summary>
/// Unit tests for <see cref="CryptographyServiceCollectionExtensions"/>.
/// </summary>
public class CryptographyServiceCollectionExtensionsTests
{
    #region Base64

    /// <summary>
    /// Tests that AddBase64Cryptography with encoding table registers the service.
    /// </summary>
    [Fact]
    public void AddBase64Cryptography_WithEncodingTable_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddBase64Cryptography(Base64EncodingTable.Standard);

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ICryptographyService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<Base64CryptographyService>();
    }

    /// <summary>
    /// Tests that AddBase64Cryptography with UrlSafe encoding table registers the service.
    /// </summary>
    [Fact]
    public void AddBase64Cryptography_WithUrlSafeEncodingTable_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddBase64Cryptography(Base64EncodingTable.UrlSafe);

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ICryptographyService>();
        service.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that AddBase64Cryptography with configure action registers the service.
    /// </summary>
    [Fact]
    public void AddBase64Cryptography_WithConfigureAction_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddBase64Cryptography(options =>
        {
            options.EncodingTable = Base64EncodingTable.UrlSafe;
            options.IncludePadding = false;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ICryptographyService>();
        service.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that AddBase64Cryptography with null services throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddBase64Cryptography_WithNullServices_ShouldThrow()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddBase64Cryptography(Base64EncodingTable.Standard);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that AddBase64Cryptography with null configure action throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddBase64Cryptography_WithNullConfigureAction_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddBase64Cryptography((Action<Base64CryptographyOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region AES

    /// <summary>
    /// Tests that AddAesCryptography with key and IV registers the service.
    /// </summary>
    [Fact]
    public void AddAesCryptography_WithKeyAndIv_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);

        // Act
        services.AddAesCryptography(key, iv);

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ICryptographyService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<AesCryptographyService>();
    }

    /// <summary>
    /// Tests that AddAesCryptography with null key throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddAesCryptography_WithNullKey_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var (_, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);

        // Act
        var act = () => services.AddAesCryptography(null!, iv);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that AddAesCryptography with null IV throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddAesCryptography_WithNullIv_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var (key, _) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);

        // Act
        var act = () => services.AddAesCryptography(key, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region RSA

    /// <summary>
    /// Tests that AddRsaCryptography with public key registers the service.
    /// </summary>
    [Fact]
    public void AddRsaCryptography_WithPublicKey_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);

        // Act
        services.AddRsaCryptography(keyPair.PublicKey!, RsaPaddingMode.OaepSha256);

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ICryptographyService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<RsaCryptographyService>();
    }

    /// <summary>
    /// Tests that AddRsaCryptography with key pair registers the service.
    /// </summary>
    [Fact]
    public void AddRsaCryptography_WithKeyPair_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);

        // Act
        services.AddRsaCryptography(keyPair, RsaPaddingMode.OaepSha256);

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ICryptographyService>();
        service.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that AddRsaCryptography with null public key throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddRsaCryptography_WithNullPublicKey_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddRsaCryptography((byte[])null!, RsaPaddingMode.OaepSha256);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that AddRsaCryptography with null key pair throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddRsaCryptography_WithNullKeyPair_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddRsaCryptography((RsaKeyPair)null!, RsaPaddingMode.OaepSha256);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
