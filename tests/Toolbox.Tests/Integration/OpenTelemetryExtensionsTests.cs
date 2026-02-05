using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="OpenTelemetryExtensions"/>.
/// </summary>
public class OpenTelemetryExtensionsTests
{
    [Fact]
    public void AddToolboxInstrumentation_Tracing_ShouldAddActivitySource()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOpenTelemetry()
            .WithTracing(builder => builder.AddToolboxInstrumentation());
        var provider = services.BuildServiceProvider();

        // Assert
        var tracerProvider = provider.GetService<TracerProvider>();
        tracerProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddToolboxInstrumentation_Metrics_ShouldAddMeter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOpenTelemetry()
            .WithMetrics(builder => builder.AddToolboxInstrumentation());
        var provider = services.BuildServiceProvider();

        // Assert
        var meterProvider = provider.GetService<MeterProvider>();
        meterProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddToolboxOpenTelemetry_ShouldRegisterTelemetryOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddToolboxOpenTelemetry();
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<IOptions<ToolboxTelemetryOptions>>();
        options.Should().NotBeNull();
    }

    [Fact]
    public void AddToolboxOpenTelemetry_WithCustomOptions_ShouldApplyConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddToolboxOpenTelemetry(options =>
        {
            options.EnableTracing = false;
            options.EnableMetrics = false;
            options.ServiceName = "CustomService";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<IOptions<ToolboxTelemetryOptions>>().Value;
        options.EnableTracing.Should().BeFalse();
        options.EnableMetrics.Should().BeFalse();
        options.ServiceName.Should().Be("CustomService");
    }

    [Fact]
    public void AddToolboxInstrumentation_WithNullBuilder_ShouldThrowArgumentNullException()
    {
        // Arrange
        TracerProviderBuilder builder = null!;

        // Act
        var act = () => builder.AddToolboxInstrumentation();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddToolboxOpenTelemetry_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddToolboxOpenTelemetry();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
