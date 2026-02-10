using Microsoft.Extensions.DependencyInjection;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;
using Toolbox.Core.Services.FileTransfer;

namespace Toolbox.Tests.Unit.Extensions;

/// <summary>
/// Unit tests for <see cref="FileTransferServiceCollectionExtensions"/>.
/// </summary>
public class FileTransferServiceCollectionExtensionsTests
{
    #region FTP

    /// <summary>
    /// Tests that AddFtpFileTransfer with configure action registers the service.
    /// </summary>
    [Fact]
    public void AddFtpFileTransfer_WithConfigureAction_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddFtpFileTransfer(options =>
        {
            options.Host = "ftp.example.com";
            options.Username = "user";
            options.Password = "pass";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IFileTransferService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<FtpFileTransferService>();
    }

    /// <summary>
    /// Tests that AddFtpFileTransfer with null services throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddFtpFileTransfer_WithNullServices_ShouldThrow()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddFtpFileTransfer(options => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that AddFtpFileTransfer with null configure action throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddFtpFileTransfer_WithNullConfigureAction_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddFtpFileTransfer((Action<FileTransferOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region SFTP

    /// <summary>
    /// Tests that AddSftpFileTransfer with configure action registers the service.
    /// </summary>
    [Fact]
    public void AddSftpFileTransfer_WithConfigureAction_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSftpFileTransfer(options =>
        {
            options.Host = "sftp.example.com";
            options.Username = "user";
            options.Password = "pass";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IFileTransferService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<SftpFileTransferService>();
    }

    /// <summary>
    /// Tests that AddSftpFileTransfer with null services throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddSftpFileTransfer_WithNullServices_ShouldThrow()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddSftpFileTransfer(options => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that AddSftpFileTransfer with null configure action throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddSftpFileTransfer_WithNullConfigureAction_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddSftpFileTransfer((Action<FileTransferOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region AddFileTransfer (Auto-select)

    /// <summary>
    /// Tests that AddFileTransfer with FTP protocol registers FTP service.
    /// </summary>
    [Fact]
    public void AddFileTransfer_WithFtpProtocol_ShouldRegisterFtpService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddFileTransfer(options =>
        {
            options.Host = "ftp.example.com";
            options.Username = "user";
            options.Password = "pass";
            options.Protocol = FileTransferProtocol.Ftp;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IFileTransferService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<FtpFileTransferService>();
    }

    /// <summary>
    /// Tests that AddFileTransfer with SFTP protocol registers SFTP service.
    /// </summary>
    [Fact]
    public void AddFileTransfer_WithSftpProtocol_ShouldRegisterSftpService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddFileTransfer(options =>
        {
            options.Host = "sftp.example.com";
            options.Username = "user";
            options.Password = "pass";
            options.Protocol = FileTransferProtocol.Sftp;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IFileTransferService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<SftpFileTransferService>();
    }

    /// <summary>
    /// Tests that AddFileTransfer with null services throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void AddFileTransfer_WithNullServices_ShouldThrow()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddFileTransfer(options => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
