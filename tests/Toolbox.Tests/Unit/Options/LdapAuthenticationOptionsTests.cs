using Toolbox.Core.Options;

namespace Toolbox.Tests.Unit.Options;

/// <summary>
/// Unit tests for <see cref="LdapAuthenticationOptions"/>.
/// </summary>
public class LdapAuthenticationOptionsTests
{
    #region Default Values Tests

    /// <summary>
    /// Tests that default Mode is Simple.
    /// </summary>
    [Fact]
    public void Mode_ShouldDefaultToSimple()
    {
        // Act
        var options = new LdapAuthenticationOptions();

        // Assert
        options.Mode.Should().Be(LdapAuthenticationMode.Simple);
    }

    /// <summary>
    /// Tests that default Timeout is 30 seconds.
    /// </summary>
    [Fact]
    public void Timeout_ShouldDefaultTo30Seconds()
    {
        // Act
        var options = new LdapAuthenticationOptions();

        // Assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Tests that default IncludeGroups is false.
    /// </summary>
    [Fact]
    public void IncludeGroups_ShouldDefaultToFalse()
    {
        // Act
        var options = new LdapAuthenticationOptions();

        // Assert
        options.IncludeGroups.Should().BeFalse();
    }

    /// <summary>
    /// Tests that default IncludeClaims is false.
    /// </summary>
    [Fact]
    public void IncludeClaims_ShouldDefaultToFalse()
    {
        // Act
        var options = new LdapAuthenticationOptions();

        // Assert
        options.IncludeClaims.Should().BeFalse();
    }

    /// <summary>
    /// Tests that ClaimAttributes is initialized as empty list.
    /// </summary>
    [Fact]
    public void ClaimAttributes_ShouldBeEmptyList()
    {
        // Act
        var options = new LdapAuthenticationOptions();

        // Assert
        options.ClaimAttributes.Should().NotBeNull();
        options.ClaimAttributes.Should().BeEmpty();
    }

    #endregion

    #region Validate Tests

    /// <summary>
    /// Tests that Validate throws for Simple mode without username.
    /// </summary>
    [Fact]
    public void Validate_SimpleMode_WithoutUsername_ShouldThrow()
    {
        // Arrange
        var options = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.Simple,
            Password = "password"
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Username*");
    }

    /// <summary>
    /// Tests that Validate throws for Simple mode without password.
    /// </summary>
    [Fact]
    public void Validate_SimpleMode_WithoutPassword_ShouldThrow()
    {
        // Arrange
        var options = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.Simple,
            Username = "testuser"
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Password*");
    }

    /// <summary>
    /// Tests that Validate succeeds for Simple mode with credentials.
    /// </summary>
    [Fact]
    public void Validate_SimpleMode_WithCredentials_ShouldSucceed()
    {
        // Arrange
        var options = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.Simple,
            Username = "testuser",
            Password = "password"
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Tests that Validate throws for Certificate mode without certificate.
    /// </summary>
    [Fact]
    public void Validate_CertificateMode_WithoutCertificate_ShouldThrow()
    {
        // Arrange
        var options = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.Certificate
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Certificate*");
    }

    /// <summary>
    /// Tests that Validate succeeds for Certificate mode with certificate path.
    /// </summary>
    [Fact]
    public void Validate_CertificateMode_WithCertificatePath_ShouldSucceed()
    {
        // Arrange
        var options = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.Certificate,
            CertificatePath = "C:\\certs\\client.pfx"
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Tests that Validate succeeds for Anonymous mode without credentials.
    /// </summary>
    [Fact]
    public void Validate_AnonymousMode_WithoutCredentials_ShouldSucceed()
    {
        // Arrange
        var options = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.Anonymous
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Tests that Validate succeeds for Kerberos mode without credentials (uses current context).
    /// </summary>
    [Fact]
    public void Validate_KerberosMode_WithoutCredentials_ShouldSucceed()
    {
        // Arrange
        var options = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.Kerberos
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Tests that Validate succeeds for IntegratedWindows mode.
    /// </summary>
    [Fact]
    public void Validate_IntegratedWindowsMode_ShouldSucceed()
    {
        // Arrange
        var options = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.IntegratedWindows
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Tests that Validate throws for SaslPlain without username.
    /// </summary>
    [Fact]
    public void Validate_SaslPlainMode_WithoutUsername_ShouldThrow()
    {
        // Arrange
        var options = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.SaslPlain,
            Password = "password"
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Tests that Validate throws for SaslExternal without certificate.
    /// </summary>
    [Fact]
    public void Validate_SaslExternalMode_WithoutCertificate_ShouldThrow()
    {
        // Arrange
        var options = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.SaslExternal
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region GetCertificate Tests

    /// <summary>
    /// Tests that GetCertificate returns null when no certificate is configured.
    /// </summary>
    [Fact]
    public void GetCertificate_WithNoCertificateConfigured_ShouldReturnNull()
    {
        // Arrange
        var options = new LdapAuthenticationOptions();

        // Act
        var cert = options.GetCertificate();

        // Assert
        cert.Should().BeNull();
    }

    /// <summary>
    /// Tests that GetCertificate throws FileNotFoundException for non-existent file.
    /// </summary>
    [Fact]
    public void GetCertificate_WithNonExistentPath_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var options = new LdapAuthenticationOptions
        {
            CertificatePath = "C:\\non\\existent\\path\\cert.pfx"
        };

        // Act
        var act = () => options.GetCertificate();

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Tests that all properties can be set and retrieved.
    /// </summary>
    [Fact]
    public void AllProperties_ShouldBeSettable()
    {
        // Arrange & Act
        var options = new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.Kerberos,
            Username = "testuser",
            Password = "password",
            Domain = "EXAMPLE",
            CertificatePath = "C:\\certs\\client.pfx",
            CertificatePassword = "certpass",
            IncludeGroups = true,
            IncludeClaims = true,
            Timeout = TimeSpan.FromMinutes(2),
            SaslMechanism = "GSSAPI",
            ServicePrincipalName = "LDAP/dc.example.com"
        };
        options.ClaimAttributes.Add("department");
        options.ClaimAttributes.Add("title");

        // Assert
        options.Mode.Should().Be(LdapAuthenticationMode.Kerberos);
        options.Username.Should().Be("testuser");
        options.Password.Should().Be("password");
        options.Domain.Should().Be("EXAMPLE");
        options.CertificatePath.Should().Be("C:\\certs\\client.pfx");
        options.CertificatePassword.Should().Be("certpass");
        options.IncludeGroups.Should().BeTrue();
        options.IncludeClaims.Should().BeTrue();
        options.Timeout.Should().Be(TimeSpan.FromMinutes(2));
        options.SaslMechanism.Should().Be("GSSAPI");
        options.ServicePrincipalName.Should().Be("LDAP/dc.example.com");
        options.ClaimAttributes.Should().Contain("department");
        options.ClaimAttributes.Should().Contain("title");
    }

    #endregion
}
