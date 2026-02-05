using Toolbox.Core.Options;

namespace Toolbox.Tests.Regression;

/// <summary>
/// Regression tests for options classes to detect breaking changes in configuration schema.
/// </summary>
public class OptionsSchemaTests
{
    [Fact]
    public void ToolboxOptions_SectionName_ShouldNotChange()
    {
        ToolboxOptions.SectionName.Should().Be("Toolbox");
    }

    [Fact]
    public void ToolboxOptions_DefaultValues_ShouldNotChange()
    {
        var options = new ToolboxOptions();

        options.EnableDetailedTelemetry.Should().BeFalse();
        options.ServicePrefix.Should().BeNull();
        options.AsyncDisposalTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ToolboxTelemetryOptions_SectionName_ShouldNotChange()
    {
        ToolboxTelemetryOptions.SectionName.Should().Be("Toolbox:Telemetry");
    }

    [Fact]
    public void ToolboxTelemetryOptions_DefaultValues_ShouldNotChange()
    {
        var options = new ToolboxTelemetryOptions();

        options.EnableTracing.Should().BeTrue();
        options.EnableMetrics.Should().BeTrue();
        options.EnableConsoleExport.Should().BeFalse();
        options.OtlpEndpoint.Should().BeNull();
        options.ServiceName.Should().Be("Toolbox");
        options.ServiceVersion.Should().Be("1.0.0");
    }
}
