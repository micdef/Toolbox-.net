using Microsoft.Extensions.DependencyInjection;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Mailing;

namespace Toolbox.Tests.Unit.Extensions;

/// <summary>
/// Unit tests for <see cref="MailingServiceCollectionExtensions"/>.
/// </summary>
public class MailingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSmtpMailing_WithConfigureAction_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSmtpMailing(options =>
        {
            options.Host = "smtp.example.com";
            options.Port = 587;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IMailingService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<SmtpMailingService>();
    }

    [Fact]
    public void AddSmtpMailing_WithOptions_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var options = new MailingOptions
        {
            Host = "smtp.example.com",
            Port = 587
        };

        // Act
        services.AddSmtpMailing(options);

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IMailingService>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddSmtpMailing_WithNullServices_ShouldThrow()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddSmtpMailing(options => { options.Host = "test"; });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSmtpMailing_WithNullConfigureAction_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddSmtpMailing((Action<MailingOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSmtpMailing_WithNullOptions_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddSmtpMailing((MailingOptions)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGmailMailing_ShouldConfigureCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddGmailMailing("test@gmail.com", "app-password");

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IMailingService>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddGmailMailing_WithNullEmail_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddGmailMailing(null!, "password");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGmailMailing_WithNullPassword_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddGmailMailing("test@gmail.com", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddOutlookMailing_ShouldConfigureCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOutlookMailing("test@outlook.com", "password");

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IMailingService>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddOutlookMailing_WithNullEmail_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddOutlookMailing(null!, "password");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddOutlookMailing_WithNullPassword_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddOutlookMailing("test@outlook.com", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSmtpMailing_MultipleCalls_ShouldNotDuplicate()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSmtpMailing(options => { options.Host = "smtp1.example.com"; });
        services.AddSmtpMailing(options => { options.Host = "smtp2.example.com"; });

        // Assert
        var descriptors = services.Where(d => d.ServiceType == typeof(IMailingService)).ToList();
        descriptors.Should().HaveCount(1);
    }
}
