using Toolbox.Core.Options;

namespace Toolbox.Tests.Unit.Options;

/// <summary>
/// Unit tests for <see cref="LdapAuthenticationResult"/>.
/// </summary>
public class LdapAuthenticationResultTests
{
    #region Success Factory Method Tests

    /// <summary>
    /// Tests that Success factory method creates a successful authentication result.
    /// </summary>
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = LdapAuthenticationResult.Success(
            "testuser",
            LdapAuthenticationMode.Simple,
            LdapDirectoryType.ActiveDirectory);

        // Assert
        result.IsAuthenticated.Should().BeTrue();
        result.Username.Should().Be("testuser");
        result.AuthenticationMode.Should().Be(LdapAuthenticationMode.Simple);
        result.DirectoryType.Should().Be(LdapDirectoryType.ActiveDirectory);
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
        result.AuthenticatedAt.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that Success sets AuthenticatedAt to current time.
    /// </summary>
    [Fact]
    public void Success_ShouldSetAuthenticatedAtToCurrentTime()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = LdapAuthenticationResult.Success(
            "testuser",
            LdapAuthenticationMode.Simple,
            LdapDirectoryType.OpenLdap);

        // Assert
        var after = DateTimeOffset.UtcNow;
        result.AuthenticatedAt.Should().BeOnOrAfter(before);
        result.AuthenticatedAt.Should().BeOnOrBefore(after);
    }

    #endregion

    #region Failure Factory Method Tests

    /// <summary>
    /// Tests that Failure factory method creates a failed authentication result.
    /// </summary>
    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        // Act
        var result = LdapAuthenticationResult.Failure(
            "Invalid credentials",
            "49",
            LdapAuthenticationMode.Simple,
            LdapDirectoryType.ActiveDirectory);

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid credentials");
        result.ErrorCode.Should().Be("49");
        result.AuthenticationMode.Should().Be(LdapAuthenticationMode.Simple);
        result.DirectoryType.Should().Be(LdapDirectoryType.ActiveDirectory);
        result.Username.Should().BeNull();
        result.AuthenticatedAt.Should().BeNull();
    }

    /// <summary>
    /// Tests that Failure without error code works correctly.
    /// </summary>
    [Fact]
    public void Failure_WithoutErrorCode_ShouldCreateFailedResult()
    {
        // Act
        var result = LdapAuthenticationResult.Failure(
            "Connection timeout",
            null,
            LdapAuthenticationMode.Kerberos,
            LdapDirectoryType.ActiveDirectory);

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.ErrorMessage.Should().Be("Connection timeout");
        result.ErrorCode.Should().BeNull();
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Tests that Groups can be set via object initializer.
    /// </summary>
    [Fact]
    public void Groups_ShouldBeSettableViaInitializer()
    {
        // Arrange
        var groups = new List<string> { "Domain Users", "Developers", "Admins" };

        // Act
        var result = new LdapAuthenticationResult
        {
            IsAuthenticated = true,
            Username = "testuser",
            AuthenticationMode = LdapAuthenticationMode.Simple,
            DirectoryType = LdapDirectoryType.ActiveDirectory,
            Groups = groups
        };

        // Assert
        result.Groups.Should().BeEquivalentTo(groups);
    }

    /// <summary>
    /// Tests that Claims dictionary can be set via object initializer.
    /// </summary>
    [Fact]
    public void Claims_ShouldBeSettableViaInitializer()
    {
        // Arrange
        var claims = new Dictionary<string, object>
        {
            ["email"] = "test@example.com",
            ["department"] = "Engineering"
        };

        // Act
        var result = new LdapAuthenticationResult
        {
            IsAuthenticated = true,
            Username = "testuser",
            AuthenticationMode = LdapAuthenticationMode.Simple,
            DirectoryType = LdapDirectoryType.AzureActiveDirectory,
            Claims = claims
        };

        // Assert
        result.Claims.Should().ContainKey("email");
        result.Claims!["email"].Should().Be("test@example.com");
        result.Claims.Should().ContainKey("department");
    }

    /// <summary>
    /// Tests that Token and ExpiresAt can be set for OAuth flows.
    /// </summary>
    [Fact]
    public void Token_ShouldBeSettableForOAuthFlows()
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        var result = new LdapAuthenticationResult
        {
            IsAuthenticated = true,
            Username = "testuser",
            AuthenticationMode = LdapAuthenticationMode.Simple,
            DirectoryType = LdapDirectoryType.AzureActiveDirectory,
            Token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9...",
            ExpiresAt = expiresAt
        };

        // Assert
        result.Token.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().Be(expiresAt);
    }

    /// <summary>
    /// Tests that UserDistinguishedName can be set via object initializer.
    /// </summary>
    [Fact]
    public void UserDistinguishedName_ShouldBeSettableViaInitializer()
    {
        // Act
        var result = new LdapAuthenticationResult
        {
            IsAuthenticated = true,
            Username = "testuser",
            AuthenticationMode = LdapAuthenticationMode.Kerberos,
            DirectoryType = LdapDirectoryType.ActiveDirectory,
            UserDistinguishedName = "CN=testuser,OU=Users,DC=example,DC=com"
        };

        // Assert
        result.UserDistinguishedName.Should().Be("CN=testuser,OU=Users,DC=example,DC=com");
    }

    #endregion

    #region DirectoryType Tests

    /// <summary>
    /// Tests that all directory types are represented correctly.
    /// </summary>
    [Theory]
    [InlineData(LdapDirectoryType.ActiveDirectory)]
    [InlineData(LdapDirectoryType.OpenLdap)]
    [InlineData(LdapDirectoryType.AppleDirectory)]
    [InlineData(LdapDirectoryType.AzureActiveDirectory)]
    public void Success_WithDifferentDirectoryTypes_ShouldSetCorrectType(LdapDirectoryType directoryType)
    {
        // Act
        var result = LdapAuthenticationResult.Success(
            "testuser",
            LdapAuthenticationMode.Simple,
            directoryType);

        // Assert
        result.DirectoryType.Should().Be(directoryType);
    }

    #endregion
}
