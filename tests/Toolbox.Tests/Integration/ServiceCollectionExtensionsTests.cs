using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

namespace Toolbox.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ServiceCollectionExtensions"/>.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddToolboxCore_ShouldRegisterOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddToolboxCore();
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<IOptions<ToolboxOptions>>();
        options.Should().NotBeNull();
    }

    [Fact]
    public void AddToolboxCore_WithConfiguration_ShouldApplyOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddToolboxCore(options =>
        {
            options.EnableDetailedTelemetry = true;
            options.ServicePrefix = "TestPrefix";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<IOptions<ToolboxOptions>>().Value;
        options.EnableDetailedTelemetry.Should().BeTrue();
        options.ServicePrefix.Should().Be("TestPrefix");
    }

    [Fact]
    public void AddToolboxCore_FromConfiguration_ShouldBindOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Toolbox:EnableDetailedTelemetry"] = "true",
                ["Toolbox:ServicePrefix"] = "ConfigPrefix"
            })
            .Build();

        // Act
        services.AddToolboxCore(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<IOptions<ToolboxOptions>>().Value;
        options.EnableDetailedTelemetry.Should().BeTrue();
        options.ServicePrefix.Should().Be("ConfigPrefix");
    }

    [Fact]
    public void AddToolboxCore_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddToolboxCore();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddToolboxCore_WithNullConfigureAction_ShouldThrowArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddToolboxCore((Action<ToolboxOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
