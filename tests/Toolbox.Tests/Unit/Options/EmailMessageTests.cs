using Toolbox.Core.Options;

namespace Toolbox.Tests.Unit.Options;

/// <summary>
/// Unit tests for <see cref="EmailMessage"/> and related classes.
/// </summary>
public class EmailMessageTests
{
    #region EmailAddress Tests

    [Fact]
    public void EmailAddress_WithAddressOnly_ShouldCreate()
    {
        // Arrange & Act
        var address = new EmailAddress("test@example.com");

        // Assert
        address.Address.Should().Be("test@example.com");
        address.DisplayName.Should().BeNull();
    }

    [Fact]
    public void EmailAddress_WithDisplayName_ShouldCreate()
    {
        // Arrange & Act
        var address = new EmailAddress("test@example.com", "Test User");

        // Assert
        address.Address.Should().Be("test@example.com");
        address.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public void EmailAddress_WithNullAddress_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new EmailAddress(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EmailAddress_ImplicitConversion_ShouldWork()
    {
        // Arrange & Act
        EmailAddress address = "test@example.com";

        // Assert
        address.Address.Should().Be("test@example.com");
        address.DisplayName.Should().BeNull();
    }

    [Fact]
    public void EmailAddress_ToString_WithoutDisplayName_ShouldReturnAddress()
    {
        // Arrange
        var address = new EmailAddress("test@example.com");

        // Act
        var result = address.ToString();

        // Assert
        result.Should().Be("test@example.com");
    }

    [Fact]
    public void EmailAddress_ToString_WithDisplayName_ShouldReturnFormattedAddress()
    {
        // Arrange
        var address = new EmailAddress("test@example.com", "Test User");

        // Act
        var result = address.ToString();

        // Assert
        result.Should().Be("\"Test User\" <test@example.com>");
    }

    #endregion

    #region EmailMessage Tests

    [Fact]
    public void EmailMessage_NewInstance_ShouldHaveEmptyCollections()
    {
        // Arrange & Act
        var message = new EmailMessage();

        // Assert
        message.To.Should().BeEmpty();
        message.Cc.Should().BeEmpty();
        message.Bcc.Should().BeEmpty();
        message.Attachments.Should().BeEmpty();
        message.Headers.Should().BeEmpty();
    }

    [Fact]
    public void EmailMessage_NewInstance_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var message = new EmailMessage();

        // Assert
        message.Subject.Should().BeEmpty();
        message.Body.Should().BeEmpty();
        message.IsBodyHtml.Should().BeFalse();
        message.Priority.Should().Be(EmailPriority.Normal);
    }

    [Fact]
    public void EmailMessage_HasRecipients_WithNoRecipients_ShouldReturnFalse()
    {
        // Arrange
        var message = new EmailMessage();

        // Act & Assert
        message.HasRecipients.Should().BeFalse();
    }

    [Fact]
    public void EmailMessage_HasRecipients_WithToRecipient_ShouldReturnTrue()
    {
        // Arrange
        var message = new EmailMessage();
        message.To.Add(new EmailAddress("test@example.com"));

        // Act & Assert
        message.HasRecipients.Should().BeTrue();
    }

    [Fact]
    public void EmailMessage_HasRecipients_WithBccOnly_ShouldReturnTrue()
    {
        // Arrange
        var message = new EmailMessage();
        message.Bcc.Add(new EmailAddress("test@example.com"));

        // Act & Assert
        message.HasRecipients.Should().BeTrue();
    }

    [Fact]
    public void EmailMessage_HasRecipients_WithCcOnly_ShouldReturnFalse()
    {
        // Arrange
        var message = new EmailMessage();
        message.Cc.Add(new EmailAddress("test@example.com"));

        // Act & Assert
        message.HasRecipients.Should().BeFalse();
    }

    [Fact]
    public void EmailMessage_TotalRecipients_ShouldCountAll()
    {
        // Arrange
        var message = new EmailMessage();
        message.To.Add(new EmailAddress("to1@example.com"));
        message.To.Add(new EmailAddress("to2@example.com"));
        message.Cc.Add(new EmailAddress("cc@example.com"));
        message.Bcc.Add(new EmailAddress("bcc@example.com"));

        // Act & Assert
        message.TotalRecipients.Should().Be(4);
    }

    [Fact]
    public void EmailMessage_Validate_WithNoRecipients_ShouldThrow()
    {
        // Arrange
        var message = new EmailMessage();

        // Act
        var act = () => message.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*recipient*");
    }

    [Fact]
    public void EmailMessage_Validate_WithToRecipient_ShouldNotThrow()
    {
        // Arrange
        var message = new EmailMessage();
        message.To.Add(new EmailAddress("test@example.com"));

        // Act
        var act = () => message.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EmailMessage_Validate_WithBccOnly_ShouldNotThrow()
    {
        // Arrange
        var message = new EmailMessage();
        message.Bcc.Add(new EmailAddress("test@example.com"));

        // Act
        var act = () => message.Validate();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region EmailAttachment Tests

    [Fact]
    public void EmailAttachment_FromFile_WithNonExistentFile_ShouldThrow()
    {
        // Act
        var act = () => EmailAttachment.FromFile("nonexistent.pdf");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void EmailAttachment_FromFile_WithNullPath_ShouldThrow()
    {
        // Act
        var act = () => EmailAttachment.FromFile(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EmailAttachment_FromBytes_WithValidData_ShouldCreate()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var attachment = EmailAttachment.FromBytes(content, "test.pdf");

        // Assert
        attachment.FileName.Should().Be("test.pdf");
        attachment.ContentType.Should().Be("application/pdf");
        attachment.Content.Should().BeEquivalentTo(content);
        attachment.FilePath.Should().BeNull();
        attachment.IsInline.Should().BeFalse();
    }

    [Fact]
    public void EmailAttachment_FromBytes_WithNullContent_ShouldThrow()
    {
        // Act
        var act = () => EmailAttachment.FromBytes(null!, "test.pdf");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EmailAttachment_FromBytes_WithNullFileName_ShouldThrow()
    {
        // Act
        var act = () => EmailAttachment.FromBytes(new byte[] { 1, 2, 3 }, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EmailAttachment_FromBytes_WithCustomContentType_ShouldUseIt()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3 };

        // Act
        var attachment = EmailAttachment.FromBytes(content, "data.bin", "application/custom");

        // Assert
        attachment.ContentType.Should().Be("application/custom");
    }

    [Fact]
    public void EmailAttachment_CreateInline_ShouldCreateInlineAttachment()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3 };

        // Act
        var attachment = EmailAttachment.CreateInline(content, "logo.png", "company-logo");

        // Assert
        attachment.FileName.Should().Be("logo.png");
        attachment.ContentType.Should().Be("image/png");
        attachment.IsInline.Should().BeTrue();
        attachment.ContentId.Should().Be("company-logo");
    }

    [Theory]
    [InlineData(".pdf", "application/pdf")]
    [InlineData(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".txt", "text/plain")]
    [InlineData(".html", "text/html")]
    [InlineData(".json", "application/json")]
    [InlineData(".xml", "application/xml")]
    [InlineData(".zip", "application/zip")]
    [InlineData(".unknown", "application/octet-stream")]
    public void EmailAttachment_FromBytes_ShouldDetectContentType(string extension, string expectedContentType)
    {
        // Arrange
        var content = new byte[] { 1, 2, 3 };

        // Act
        var attachment = EmailAttachment.FromBytes(content, $"file{extension}");

        // Assert
        attachment.ContentType.Should().Be(expectedContentType);
    }

    #endregion

    #region MailingOptions Tests

    [Fact]
    public void MailingOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new MailingOptions();

        // Assert
        options.Host.Should().Be("localhost");
        options.Port.Should().Be(25);
        options.SecurityMode.Should().Be(SmtpSecurityMode.Auto);
        options.Username.Should().BeNull();
        options.Password.Should().BeNull();
        options.OAuth2AccessToken.Should().BeNull();
        options.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.OperationTimeout.Should().Be(TimeSpan.FromMinutes(2));
        options.ValidateCertificate.Should().BeTrue();
        options.DefaultFrom.Should().BeNull();
        options.DefaultReplyTo.Should().BeNull();
    }

    [Fact]
    public void MailingOptions_RequiresAuthentication_WithNoCredentials_ShouldReturnFalse()
    {
        // Arrange
        var options = new MailingOptions();

        // Act & Assert
        options.RequiresAuthentication.Should().BeFalse();
    }

    [Fact]
    public void MailingOptions_RequiresAuthentication_WithUsername_ShouldReturnTrue()
    {
        // Arrange
        var options = new MailingOptions { Username = "user" };

        // Act & Assert
        options.RequiresAuthentication.Should().BeTrue();
    }

    [Fact]
    public void MailingOptions_RequiresAuthentication_WithOAuth2_ShouldReturnTrue()
    {
        // Arrange
        var options = new MailingOptions { OAuth2AccessToken = "token" };

        // Act & Assert
        options.RequiresAuthentication.Should().BeTrue();
    }

    #endregion
}
