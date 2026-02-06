using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Api;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Toolbox.Tests.Unit.Services.Api;

/// <summary>
/// Unit tests for <see cref="HttpApiService"/>.
/// </summary>
/// <remarks>
/// These tests verify argument validation and configuration.
/// Integration tests with real APIs would be in a separate test project.
/// </remarks>
public class HttpApiServiceTests
{
    private readonly Mock<ILogger<HttpApiService>> _loggerMock;

    public HttpApiServiceTests()
    {
        _loggerMock = new Mock<ILogger<HttpApiService>>();
    }

    private static ApiOptions CreateValidOptions() => new()
    {
        BaseUrl = "https://api.example.com",
        AuthenticationMode = ApiAuthenticationMode.Anonymous,
        Timeout = TimeSpan.FromSeconds(30)
    };

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new HttpApiService(
            (IOptions<ApiOptions>)null!,
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
        var act = () => new HttpApiService(options, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldInitialize()
    {
        // Arrange
        var options = MsOptions.Create(CreateValidOptions());

        // Act
        using var service = new HttpApiService(options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithDirectOptions_ShouldInitialize()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        using var service = new HttpApiService(options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithBearerTokenAuth_ShouldInitialize()
    {
        // Arrange
        var options = MsOptions.Create(new ApiOptions
        {
            BaseUrl = "https://api.example.com",
            AuthenticationMode = ApiAuthenticationMode.BearerToken,
            BearerToken = "test-token"
        });

        // Act
        using var service = new HttpApiService(options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithBasicAuth_ShouldInitialize()
    {
        // Arrange
        var options = MsOptions.Create(new ApiOptions
        {
            BaseUrl = "https://api.example.com",
            AuthenticationMode = ApiAuthenticationMode.Basic,
            Username = "user",
            Password = "pass"
        });

        // Act
        using var service = new HttpApiService(options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithApiKeyAuth_ShouldInitialize()
    {
        // Arrange
        var options = MsOptions.Create(new ApiOptions
        {
            BaseUrl = "https://api.example.com",
            AuthenticationMode = ApiAuthenticationMode.ApiKey,
            ApiKey = "my-api-key",
            ApiKeyName = "X-API-Key"
        });

        // Act
        using var service = new HttpApiService(options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithHttpClient_ShouldInitialize()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var options = MsOptions.Create(CreateValidOptions());

        // Act
        using var service = new HttpApiService(httpClient, options, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region Send Tests

    [Fact]
    public void Send_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new HttpApiService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = () => service.Send(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new HttpApiService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        var act = async () => await service.SendAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var service = new HttpApiService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var request = ApiRequest.Get("/test");

        // Act
        var act = async () => await service.SendAsync(request, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Send_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new HttpApiService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);
        service.Dispose();

        var request = ApiRequest.Get("/test");

        // Act
        var act = () => service.Send(request);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeResources()
    {
        // Arrange
        var service = new HttpApiService(
            MsOptions.Create(CreateValidOptions()),
            _loggerMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert
        var request = ApiRequest.Get("/test");
        var act = () => service.Send(request);
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion
}
