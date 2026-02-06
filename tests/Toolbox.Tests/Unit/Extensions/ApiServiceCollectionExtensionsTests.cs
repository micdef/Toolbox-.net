using Microsoft.Extensions.DependencyInjection;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Api;

namespace Toolbox.Tests.Unit.Extensions;

/// <summary>
/// Unit tests for <see cref="ApiServiceCollectionExtensions"/>.
/// </summary>
public class ApiServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHttpApi_WithConfigureAction_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHttpApi(options =>
        {
            options.BaseUrl = "https://api.example.com";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IApiService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<HttpApiService>();
    }

    [Fact]
    public void AddHttpApi_WithOptions_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var options = new ApiOptions
        {
            BaseUrl = "https://api.example.com"
        };

        // Act
        services.AddHttpApi(options);

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IApiService>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddHttpApi_WithNullServices_ShouldThrow()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddHttpApi(options => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddHttpApi_WithNullConfigureAction_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddHttpApi((Action<ApiOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddHttpApiAnonymous_ShouldRegisterAnonymousService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHttpApiAnonymous("https://api.example.com");

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IApiService>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddHttpApiWithBearerToken_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHttpApiWithBearerToken("https://api.example.com", "test-token");

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IApiService>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddHttpApiWithBearerToken_WithNullToken_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddHttpApiWithBearerToken("https://api.example.com", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddHttpApiWithBasicAuth_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHttpApiWithBasicAuth("https://api.example.com", "user", "pass");

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IApiService>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddHttpApiWithBasicAuth_WithNullUsername_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddHttpApiWithBasicAuth("https://api.example.com", null!, "pass");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddHttpApiWithApiKey_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHttpApiWithApiKey("https://api.example.com", "my-api-key");

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IApiService>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddHttpApiWithApiKey_WithQueryLocation_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHttpApiWithApiKey(
            "https://api.example.com",
            "my-api-key",
            "api_key",
            ApiKeyLocation.QueryString);

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IApiService>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddHttpApiWithOAuth2_ShouldRegisterService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHttpApiWithOAuth2(
            "https://api.example.com",
            "https://auth.example.com/token",
            "client-id",
            "client-secret",
            "read write");

        // Assert
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IApiService>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddHttpApiWithOAuth2_WithNullClientId_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddHttpApiWithOAuth2(
            "https://api.example.com",
            "https://auth.example.com/token",
            null!,
            "secret");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddHttpApi_MultipleCalls_ShouldNotDuplicate()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHttpApi(options => { options.BaseUrl = "https://api1.example.com"; });
        services.AddHttpApi(options => { options.BaseUrl = "https://api2.example.com"; });

        // Assert
        var descriptors = services.Where(d => d.ServiceType == typeof(IApiService)).ToList();
        descriptors.Should().HaveCount(1);
    }
}
