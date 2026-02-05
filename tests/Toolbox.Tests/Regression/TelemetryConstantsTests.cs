using Toolbox.Core.Telemetry;

namespace Toolbox.Tests.Regression;

/// <summary>
/// Regression tests for <see cref="TelemetryConstants"/> to detect breaking changes.
/// </summary>
public class TelemetryConstantsTests
{
    [Fact]
    public void ActivitySourceName_ShouldNotChange()
    {
        TelemetryConstants.ActivitySourceName.Should().Be("Toolbox.Core");
    }

    [Fact]
    public void MeterName_ShouldNotChange()
    {
        TelemetryConstants.MeterName.Should().Be("Toolbox.Core");
    }

    [Fact]
    public void Version_ShouldNotChange()
    {
        TelemetryConstants.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void AttributeNames_ShouldNotChange()
    {
        TelemetryConstants.Attributes.ServiceName.Should().Be("toolbox.service.name");
        TelemetryConstants.Attributes.OperationName.Should().Be("toolbox.operation.name");
        TelemetryConstants.Attributes.ErrorType.Should().Be("toolbox.error.type");
        TelemetryConstants.Attributes.DisposalReason.Should().Be("toolbox.disposal.reason");
    }

    [Fact]
    public void MetricNames_ShouldNotChange()
    {
        TelemetryConstants.Metrics.OperationCount.Should().Be("toolbox.operations.count");
        TelemetryConstants.Metrics.OperationDuration.Should().Be("toolbox.operations.duration");
        TelemetryConstants.Metrics.DisposalCount.Should().Be("toolbox.disposals.count");
        TelemetryConstants.Metrics.ActiveInstances.Should().Be("toolbox.instances.active");
    }
}
