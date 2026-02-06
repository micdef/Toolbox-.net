using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toolbox.Core.Options;
using Toolbox.Core.Services.FileTransfer;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Toolbox.Tests.Unit.Services.FileTransfer;

/// <summary>
/// Unit tests for <see cref="SftpFileTransferService"/>.
/// </summary>
/// <remarks>
/// These tests verify argument validation and configuration.
/// Integration tests with a real SFTP server would be in a separate test project.
/// </remarks>
public class SftpFileTransferServiceTests
{
    private readonly Mock<ILogger<SftpFileTransferService>> _loggerMock;

    public SftpFileTransferServiceTests()
    {
        _loggerMock = new Mock<ILogger<SftpFileTransferService>>();
    }

    private static FileTransferOptions CreateValidOptions() => new()
    {
        Host = "sftp.example.com",
        Username = "testuser",
        Password = "testpass",
        Protocol = FileTransferProtocol.Sftp
    };

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new SftpFileTransferService(
            (IOptions<FileTransferOptions>)null!,
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
        var act = () => new SftpFileTransferService(options, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyHost_ShouldThrowArgumentException()
    {
        // Arrange
        var options = MsOptions.Create(new FileTransferOptions
        {
            Host = "",
            Username = "user",
            Password = "pass"
        });

        // Act
        var act = () => new SftpFileTransferService(options, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Host*");
    }

    [Fact]
    public void Constructor_WithEmptyUsername_ShouldThrowArgumentException()
    {
        // Arrange
        var options = MsOptions.Create(new FileTransferOptions
        {
            Host = "sftp.example.com",
            Username = "",
            Password = "pass"
        });

        // Act
        var act = () => new SftpFileTransferService(options, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Username*");
    }

    [Fact]
    public void Constructor_WithNoAuthentication_ShouldThrowArgumentException()
    {
        // Arrange
        var options = MsOptions.Create(new FileTransferOptions
        {
            Host = "sftp.example.com",
            Username = "user",
            Password = null,
            PrivateKeyPath = null
        });

        // Act
        var act = () => new SftpFileTransferService(options, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*authentication*");
    }

    [Fact]
    public void Constructor_WithPasswordAuth_ShouldInitialize()
    {
        // Arrange
        var options = MsOptions.Create(CreateValidOptions());

        // Act
        using var service = new SftpFileTransferService(options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithDirectOptions_ShouldInitialize()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        using var service = new SftpFileTransferService(options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region UploadOne Tests

    [Fact]
    public void UploadOne_WithNullLocalPath_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = () => service.UploadOne(null!, "/remote/file.txt");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UploadOne_WithNullRemotePath_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = () => service.UploadOne("C:\\local\\file.txt", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UploadOne_WithNonExistentLocalFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = () => service.UploadOne("C:\\nonexistent\\file.txt", "/remote/file.txt");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public async Task UploadOneAsync_WithNullLocalPath_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = async () => await service.UploadOneAsync(null!, "/remote/file.txt");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UploadOneAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Create a temporary file to avoid FileNotFoundException
        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var act = async () => await service.UploadOneAsync(tempFile, "/remote/file.txt", true, cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region UploadBatch Tests

    [Fact]
    public void UploadBatch_WithNullFiles_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = () => service.UploadBatch(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task UploadBatchAsync_WithNullFiles_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = async () => await service.UploadBatchAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region DownloadOne Tests

    [Fact]
    public void DownloadOne_WithNullRemotePath_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = () => service.DownloadOne(null!, "C:\\local\\file.txt");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DownloadOne_WithNullLocalPath_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = () => service.DownloadOne("/remote/file.txt", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DownloadOneAsync_WithNullRemotePath_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = async () => await service.DownloadOneAsync(null!, "C:\\local\\file.txt");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DownloadOneAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await service.DownloadOneAsync("/remote/file.txt", "C:\\local\\file.txt", true, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region DownloadBatch Tests

    [Fact]
    public void DownloadBatch_WithNullFiles_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = () => service.DownloadBatch(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DownloadBatchAsync_WithNullFiles_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = async () => await service.DownloadBatchAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void UploadOne_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);
        service.Dispose();

        // Act
        var act = () => service.UploadOne("C:\\file.txt", "/remote/file.txt");

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeResources()
    {
        // Arrange
        var service = new SftpFileTransferService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert
        var act = () => service.UploadOne("C:\\file.txt", "/remote/file.txt");
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion
}
