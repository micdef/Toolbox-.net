using Toolbox.Core.Options;

namespace Toolbox.Tests.Unit.Options;

/// <summary>
/// Unit tests for <see cref="FileTransferOptions"/>.
/// </summary>
public class FileTransferOptionsTests
{
    [Fact]
    public void GetDefaultPort_ForFtp_ShouldReturn21()
    {
        // Arrange
        var options = new FileTransferOptions { Protocol = FileTransferProtocol.Ftp };

        // Act
        var port = options.GetDefaultPort();

        // Assert
        port.Should().Be(21);
    }

    [Fact]
    public void GetDefaultPort_ForFtps_ShouldReturn990()
    {
        // Arrange
        var options = new FileTransferOptions { Protocol = FileTransferProtocol.Ftps };

        // Act
        var port = options.GetDefaultPort();

        // Assert
        port.Should().Be(990);
    }

    [Fact]
    public void GetDefaultPort_ForSftp_ShouldReturn22()
    {
        // Arrange
        var options = new FileTransferOptions { Protocol = FileTransferProtocol.Sftp };

        // Act
        var port = options.GetDefaultPort();

        // Assert
        port.Should().Be(22);
    }

    [Fact]
    public void GetEffectivePort_WhenPortIsSet_ShouldReturnSetPort()
    {
        // Arrange
        var options = new FileTransferOptions
        {
            Protocol = FileTransferProtocol.Sftp,
            Port = 2222
        };

        // Act
        var port = options.GetEffectivePort();

        // Assert
        port.Should().Be(2222);
    }

    [Fact]
    public void GetEffectivePort_WhenPortIsNotSet_ShouldReturnDefaultPort()
    {
        // Arrange
        var options = new FileTransferOptions
        {
            Protocol = FileTransferProtocol.Sftp,
            Port = 0
        };

        // Act
        var port = options.GetEffectivePort();

        // Assert
        port.Should().Be(22);
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new FileTransferOptions();

        // Assert
        options.Host.Should().BeEmpty();
        options.Username.Should().BeEmpty();
        options.Password.Should().BeNull();
        options.PrivateKeyPath.Should().BeNull();
        options.PrivateKeyContent.Should().BeNull();
        options.PrivateKeyPassphrase.Should().BeNull();
        options.Protocol.Should().Be(FileTransferProtocol.Sftp);
        options.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.OperationTimeout.Should().Be(TimeSpan.FromSeconds(60));
        options.AutoCreateDirectory.Should().BeTrue();
        options.BufferSize.Should().Be(32 * 1024);
    }

    [Fact]
    public void SectionName_ShouldBeCorrect()
    {
        // Assert
        FileTransferOptions.SectionName.Should().Be("Toolbox:FileTransfer");
    }
}
