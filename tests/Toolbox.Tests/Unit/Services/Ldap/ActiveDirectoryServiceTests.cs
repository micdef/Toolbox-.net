using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Ldap;

namespace Toolbox.Tests.Unit.Services.Ldap;

/// <summary>
/// Unit tests for <see cref="ActiveDirectoryService"/>.
/// </summary>
public class ActiveDirectoryServiceTests
{
    private readonly Mock<ILogger<ActiveDirectoryService>> _loggerMock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveDirectoryServiceTests"/> class.
    /// </summary>
    public ActiveDirectoryServiceTests()
    {
        _loggerMock = new Mock<ILogger<ActiveDirectoryService>>();
    }

    #region Constructor Tests

    /// <summary>
    /// Tests that the constructor with null options throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new ActiveDirectoryService(
            (IOptions<ActiveDirectoryOptions>)null!,
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
        var options = new ActiveDirectoryOptions { Domain = "test.local" };

        // Act
        var act = () => new ActiveDirectoryService(options, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that the constructor with empty domain throws ArgumentException.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyDomain_ShouldThrowArgumentException()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "" };

        // Act
        var act = () => new ActiveDirectoryService(options, _loggerMock.Object);

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
        var options = new ActiveDirectoryOptions { Domain = "test.local" };

        // Act
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

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
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

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
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        var service = new ActiveDirectoryService(options, _loggerMock.Object);
        await service.DisposeAsync();

        // Act
        var act = async () => await service.GetUserByUsernameAsync("testuser");

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    /// <summary>
    /// Tests that GetUserByUsernameAsync with cancellation throws OperationCanceledException.
    /// </summary>
    [Fact]
    public async Task GetUserByUsernameAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await service.GetUserByUsernameAsync("testuser", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
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
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.GetUserByEmailAsync(null!);

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
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.SearchUsersAsync((string)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that SearchUsersAsync with null criteria throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task SearchUsersAsync_WithNullCriteria_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.SearchUsersAsync((LdapSearchCriteria)null!);

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
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

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
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.ValidateCredentialsAsync("username", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region GetUserGroups Tests

    /// <summary>
    /// Tests that GetUserGroupsAsync with null username throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task GetUserGroupsAsync_WithNullUsername_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.GetUserGroupsAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Group Search Tests

    /// <summary>
    /// Tests that GetGroupByNameAsync with null group name throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task GetGroupByNameAsync_WithNullGroupName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.GetGroupByNameAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that GetGroupByDistinguishedNameAsync with null DN throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task GetGroupByDistinguishedNameAsync_WithNullDn_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.GetGroupByDistinguishedNameAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that SearchGroupsAsync with null filter throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task SearchGroupsAsync_WithNullFilter_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.SearchGroupsAsync((string)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Computer Search Tests

    /// <summary>
    /// Tests that GetComputerByNameAsync with null computer name throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task GetComputerByNameAsync_WithNullComputerName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.GetComputerByNameAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that GetComputerByDistinguishedNameAsync with null DN throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task GetComputerByDistinguishedNameAsync_WithNullDn_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.GetComputerByDistinguishedNameAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that SearchComputersAsync with null filter throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task SearchComputersAsync_WithNullFilter_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.SearchComputersAsync((string)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region GetGroupMembers Tests

    /// <summary>
    /// Tests that GetGroupMembersAsync with null group DN throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task GetGroupMembersAsync_WithNullGroupDn_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        using var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        var act = async () => await service.GetGroupMembersAsync(null!);

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
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert - subsequent operations should throw
        var act = async () => await service.GetUserByUsernameAsync("test");
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    /// <summary>
    /// Tests that Dispose disposes resources properly.
    /// </summary>
    [Fact]
    public void Dispose_ShouldDisposeResources()
    {
        // Arrange
        var options = new ActiveDirectoryOptions { Domain = "test.local" };
        var service = new ActiveDirectoryService(options, _loggerMock.Object);

        // Act
        service.Dispose();

        // Assert - subsequent operations should throw
        var act = () => service.GetUserByUsername("test");
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion
}
