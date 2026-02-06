using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Mailing;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Toolbox.Tests.Unit.Services.Mailing;

/// <summary>
/// Unit tests for <see cref="SmtpMailingService"/>.
/// </summary>
/// <remarks>
/// These tests verify argument validation and configuration.
/// Integration tests with a real SMTP server would be in a separate test project.
/// </remarks>
public class SmtpMailingServiceTests
{
    private readonly Mock<ILogger<SmtpMailingService>> _loggerMock;

    public SmtpMailingServiceTests()
    {
        _loggerMock = new Mock<ILogger<SmtpMailingService>>();
    }

    private static MailingOptions CreateValidOptions() => new()
    {
        Host = "smtp.example.com",
        Port = 587,
        SecurityMode = SmtpSecurityMode.StartTls,
        Username = "user@example.com",
        Password = "password"
    };

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SmtpMailingService(
            (IOptions<MailingOptions>)null!,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = MsOptions.Create(CreateValidOptions());

        // Act
        var act = () => new SmtpMailingService(options, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyHost_ShouldThrowArgumentException()
    {
        // Arrange
        var options = MsOptions.Create(new MailingOptions
        {
            Host = ""
        });

        // Act
        var act = () => new SmtpMailingService(options, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*host*");
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldInitialize()
    {
        // Arrange
        var options = MsOptions.Create(CreateValidOptions());

        // Act
        using var service = new SmtpMailingService(options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithDirectOptions_ShouldInitialize()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        using var service = new SmtpMailingService(options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithAnonymousAuth_ShouldInitialize()
    {
        // Arrange
        var options = MsOptions.Create(new MailingOptions
        {
            Host = "localhost",
            Port = 25,
            SecurityMode = SmtpSecurityMode.None
        });

        // Act
        using var service = new SmtpMailingService(options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region SendMail Tests

    [Fact]
    public void SendMail_WithNullMessage_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SmtpMailingService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = () => service.SendMail(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SendMail_WithNoRecipients_ShouldThrowArgumentException()
    {
        // Arrange
        using var service = new SmtpMailingService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        var message = new EmailMessage
        {
            From = new EmailAddress("sender@example.com"),
            Subject = "Test",
            Body = "Test body"
        };

        // Act
        var act = () => service.SendMail(message);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*recipient*");
    }

    [Fact]
    public async Task SendMailAsync_WithNullMessage_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SmtpMailingService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = async () => await service.SendMailAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendMailAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var service = new SmtpMailingService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var message = new EmailMessage
        {
            From = new EmailAddress("sender@example.com"),
            Subject = "Test",
            Body = "Test body",
            To = { new EmailAddress("recipient@example.com") }
        };

        // Act
        var act = async () => await service.SendMailAsync(message, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void SendMail_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new SmtpMailingService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);
        service.Dispose();

        var message = new EmailMessage
        {
            From = new EmailAddress("sender@example.com"),
            Subject = "Test",
            Body = "Test body",
            To = { new EmailAddress("recipient@example.com") }
        };

        // Act
        var act = () => service.SendMail(message);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeResources()
    {
        // Arrange
        var service = new SmtpMailingService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert
        var message = new EmailMessage
        {
            From = new EmailAddress("sender@example.com"),
            Subject = "Test",
            Body = "Test body",
            To = { new EmailAddress("recipient@example.com") }
        };

        var act = () => service.SendMail(message);
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion
}
