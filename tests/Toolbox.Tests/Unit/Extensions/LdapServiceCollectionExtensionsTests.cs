using Microsoft.Extensions.DependencyInjection;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Ldap;

namespace Toolbox.Tests.Unit.Extensions;

/// <summary>
/// Unit tests for <see cref="LdapServiceCollectionExtensions"/>.
/// </summary>
public class LdapServiceCollectionExtensionsTests
{
    #region Active Directory

    /// <summary>
    /// Tests that AddActiveDirectory with configure action registers the service.
    /// </summary>
    [Fact]
    public void AddActiveDirectory_WithConfigureAction_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddActiveDirectory(options =>
        {
            options.Domain = "corp.example.com";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ILdapService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<ActiveDirectoryService>();
    }

    /// <summary>
    /// Tests that AddActiveDirectory with options registers the service.
    /// </summary>
    [Fact]
    public void AddActiveDirectory_WithOptions_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var options = new ActiveDirectoryOptions { Domain = "test.local" };

        // Act
        services.AddActiveDirectory(options);

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ILdapService>();
        service.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that AddActiveDirectory with null services throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddActiveDirectory_WithNullServices_ShouldThrow()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddActiveDirectory(options => { options.Domain = "test"; });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that AddActiveDirectory with null configure action throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddActiveDirectory_WithNullConfigureAction_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddActiveDirectory((Action<ActiveDirectoryOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Azure AD

    /// <summary>
    /// Tests that AddAzureAd with configure action registers the service.
    /// </summary>
    [Fact]
    public void AddAzureAd_WithConfigureAction_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAzureAd(options =>
        {
            options.TenantId = "tenant-id";
            options.ClientId = "client-id";
            options.ClientSecret = "secret";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ILdapService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<AzureAdService>();
    }

    /// <summary>
    /// Tests that AddAzureAdWithManagedIdentity registers the service.
    /// </summary>
    [Fact]
    public void AddAzureAdWithManagedIdentity_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAzureAdWithManagedIdentity("tenant-id", "client-id");

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ILdapService>();
        service.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that AddAzureAd with null services throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddAzureAd_WithNullServices_ShouldThrow()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddAzureAd(options => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that AddAzureAdWithManagedIdentity with null tenantId throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddAzureAdWithManagedIdentity_WithNullTenantId_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddAzureAdWithManagedIdentity(null!, "client-id");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region OpenLDAP

    /// <summary>
    /// Tests that AddOpenLdap with configure action registers the service.
    /// </summary>
    [Fact]
    public void AddOpenLdap_WithConfigureAction_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOpenLdap(options =>
        {
            options.Host = "ldap.example.com";
            options.BaseDn = "dc=example,dc=com";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ILdapService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<OpenLdapService>();
    }

    /// <summary>
    /// Tests that AddOpenLdap with null services throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddOpenLdap_WithNullServices_ShouldThrow()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddOpenLdap(options => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that AddOpenLdap with null configure action throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddOpenLdap_WithNullConfigureAction_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddOpenLdap((Action<OpenLdapOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Apple Directory

    /// <summary>
    /// Tests that AddAppleDirectory with configure action registers the service.
    /// </summary>
    [Fact]
    public void AddAppleDirectory_WithConfigureAction_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAppleDirectory(options =>
        {
            options.Host = "od.example.com";
            options.BaseDn = "dc=example,dc=com";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ILdapService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<AppleDirectoryService>();
    }

    /// <summary>
    /// Tests that AddAppleDirectory with null services throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddAppleDirectory_WithNullServices_ShouldThrow()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddAppleDirectory(options => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that AddAppleDirectory with null configure action throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddAppleDirectory_WithNullConfigureAction_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddAppleDirectory((Action<AppleDirectoryOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Multiple Registrations

    /// <summary>
    /// Tests that multiple AddLdapService calls do not duplicate the service registration.
    /// </summary>
    [Fact]
    public void AddLdapService_MultipleCalls_ShouldNotDuplicate()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddActiveDirectory(options => { options.Domain = "test1.local"; });
        services.AddActiveDirectory(options => { options.Domain = "test2.local"; });

        // Assert - TryAddSingleton should prevent duplicates
        var descriptors = services.Where(d => d.ServiceType == typeof(ILdapService)).ToList();
        descriptors.Should().HaveCount(1);
    }

    #endregion
}
