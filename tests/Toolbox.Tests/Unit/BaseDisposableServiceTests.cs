using Microsoft.Extensions.Logging;
using Toolbox.Core.Base;
using Toolbox.Core.Telemetry;

namespace Toolbox.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="BaseDisposableService"/>.
/// </summary>
public class BaseDisposableServiceTests
{
    private readonly Mock<ILogger> _loggerMock;

    public BaseDisposableServiceTests()
    {
        _loggerMock = new Mock<ILogger>();
    }

    [Fact]
    public void Constructor_ShouldSetServiceName()
    {
        // Arrange & Act
        using var service = new TestDisposableService("TestService", _loggerMock.Object);

        // Assert
        service.ServiceName.Should().Be("TestService");
    }

    [Fact]
    public void Constructor_WithNullServiceName_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var act = () => new TestDisposableService(null!, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceName");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var act = () => new TestDisposableService("TestService", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void IsDisposed_WhenNotDisposed_ShouldReturnFalse()
    {
        // Arrange
        using var service = new TestDisposableService("TestService", _loggerMock.Object);

        // Assert
        service.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void IsDisposed_AfterDispose_ShouldReturnTrue()
    {
        // Arrange
        var service = new TestDisposableService("TestService", _loggerMock.Object);

        // Act
        service.Dispose();

        // Assert
        service.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        var service = new TestDisposableService("TestService", _loggerMock.Object);

        // Act
        service.Dispose();
        service.Dispose();
        service.Dispose();

        // Assert
        service.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public void ThrowIfDisposed_WhenNotDisposed_ShouldNotThrow()
    {
        // Arrange
        using var service = new TestDisposableService("TestService", _loggerMock.Object);

        // Act
        var act = () => service.TestThrowIfDisposed();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfDisposed_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new TestDisposableService("TestService", _loggerMock.Object);
        service.Dispose();

        // Act
        var act = () => service.TestThrowIfDisposed();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ActivitySource_ShouldReturnToolboxActivitySource()
    {
        // Arrange
        using var service = new TestDisposableService("TestService", _loggerMock.Object);

        // Assert
        service.ActivitySource.Should().BeSameAs(ToolboxActivitySource.Instance);
    }

    [Fact]
    public void Meter_ShouldReturnToolboxMeter()
    {
        // Arrange
        using var service = new TestDisposableService("TestService", _loggerMock.Object);

        // Assert
        service.Meter.Should().BeSameAs(ToolboxMeter.Instance);
    }

    [Fact]
    public void Dispose_ShouldBeThreadSafe()
    {
        // Arrange
        var service = new TestDisposableService("TestService", _loggerMock.Object);
        var tasks = new List<Task>();

        // Act
        for (var i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => service.Dispose()));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        service.DisposeCallCount.Should().Be(1);
    }

    /// <summary>
    /// Test implementation of BaseDisposableService for testing purposes.
    /// </summary>
    private sealed class TestDisposableService : BaseDisposableService
    {
        public int DisposeCallCount { get; private set; }

        public TestDisposableService(string serviceName, ILogger logger)
            : base(serviceName, logger)
        {
        }

        public void TestThrowIfDisposed() => ThrowIfDisposed();

        protected override void Dispose(bool disposing)
        {
            // Only count if not already disposed (before base.Dispose changes state)
            if (disposing && !IsDisposed)
            {
                DisposeCallCount++;
            }
            base.Dispose(disposing);
        }
    }
}
