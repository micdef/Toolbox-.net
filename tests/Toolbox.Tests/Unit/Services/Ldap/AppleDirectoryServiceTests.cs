using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Ldap;

namespace Toolbox.Tests.Unit.Services.Ldap;

/// <summary>
/// Unit tests for <see cref="AppleDirectoryService"/>.
/// </summary>
public class AppleDirectoryServiceTests
{
    private readonly Mock<ILogger<AppleDirectoryService>> _loggerMock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppleDirectoryServiceTests"/> class.
    /// </summary>
    public AppleDirectoryServiceTests()
    {
        _loggerMock = new Mock<ILogger<AppleDirectoryService>>();
    }

    #region Constructor Tests

    /// <summary>
    /// Tests that the constructor with null options throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new AppleDirectoryService(
            (IOptions<AppleDirectoryOptions>)null!,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that the constructor with null logger throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };

        // Act
        var act = () => new AppleDirectoryService(options, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that the constructor with empty host throws ArgumentException.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyHost_ShouldThrowArgumentException()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "",
            BaseDn = "dc=example,dc=com"
        };

        // Act
        var act = () => new AppleDirectoryService(options, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Tests that the constructor with valid options succeeds.
    /// </summary>
    [Fact]
    public void Constructor_WithValidOptions_ShouldSucceed()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };

        // Act
        using var service = new AppleDirectoryService(options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region GetUserByUsername Tests

    /// <summary>
    /// Tests that GetUserByUsernameAsync with null username throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task GetUserByUsernameAsync_WithNullUsername_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };
        using var service = new AppleDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.GetUserByUsernameAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that GetUserByUsernameAsync when disposed throws ObjectDisposedException.
    /// </summary>
    [Fact]
    public async Task GetUserByUsernameAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };
        var service = new AppleDirectoryService(options, _loggerMock.Object);
        await service.DisposeAsync();

        // Act
        var act = async () => await service.GetUserByUsernameAsync("testuser");

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region GetUserByEmail Tests

    /// <summary>
    /// Tests that GetUserByEmailAsync with null email throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task GetUserByEmailAsync_WithNullEmail_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };
        using var service = new AppleDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.GetUserByEmailAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region ValidateCredentials Tests

    /// <summary>
    /// Tests that ValidateCredentialsAsync with null username throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task ValidateCredentialsAsync_WithNullUsername_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };
        using var service = new AppleDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.ValidateCredentialsAsync(null!, "password");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that ValidateCredentialsAsync with null password throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task ValidateCredentialsAsync_WithNullPassword_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };
        using var service = new AppleDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.ValidateCredentialsAsync("username", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region SearchUsers Tests

    /// <summary>
    /// Tests that SearchUsersAsync with null filter throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task SearchUsersAsync_WithNullFilter_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };
        using var service = new AppleDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.SearchUsersAsync((string)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Authentication Tests

    /// <summary>
    /// Tests that AuthenticateAsync with null options throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };
        using var service = new AppleDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.AuthenticateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that AuthenticateAsync when disposed throws ObjectDisposedException.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };
        var service = new AppleDirectoryService(options, _loggerMock.Object);
        await service.DisposeAsync();

        var authOptions = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.Simple,
            Username = "testuser",
            Password = "password"
        };

        // Act
        var act = async () => await service.AuthenticateAsync(authOptions);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    /// <summary>
    /// Tests that GetSupportedAuthenticationModes returns expected modes for Apple Directory.
    /// </summary>
    [Fact]
    public void GetSupportedAuthenticationModes_ShouldReturnExpectedModes()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };
        using var service = new AppleDirectoryService(options, _loggerMock.Object);

        // Act
        var modes = service.GetSupportedAuthenticationModes();

        // Assert
        modes.Should().NotBeEmpty();
        modes.Should().Contain(LdapAuthenticationMode.Simple);
        modes.Should().Contain(LdapAuthenticationMode.Anonymous);
        modes.Should().Contain(LdapAuthenticationMode.SaslPlain);
    }

    /// <summary>
    /// Tests that AuthenticateWithCertificateAsync with null certificate throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task AuthenticateWithCertificateAsync_WithNullCertificate_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };
        using var service = new AppleDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.AuthenticateWithCertificateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Disposal Tests

    /// <summary>
    /// Tests that DisposeAsync disposes resources properly.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_ShouldDisposeResources()
    {
        // Arrange
        var options = new AppleDirectoryOptions
        {
            Host = "od.example.com",
            BaseDn = "dc=example,dc=com"
        };
        var service = new AppleDirectoryService(options, _loggerMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert - subsequent operations should throw
        var act = async () => await service.GetUserByUsernameAsync("test");
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion
}
