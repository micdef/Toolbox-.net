using Microsoft.Extensions.Logging;
using Toolbox.Core.Base;

namespace Toolbox.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="BaseAsyncDisposableService"/>.
/// </summary>
public class BaseAsyncDisposableServiceTests
{
    private readonly Mock<ILogger> _loggerMock;

    public BaseAsyncDisposableServiceTests()
    {
        _loggerMock = new Mock<ILogger>();
    }

    [Fact]
    public async Task DisposeAsync_ShouldSetIsDisposed()
    {
        // Arrange
        var service = new TestAsyncDisposableService("TestService", _loggerMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert
        service.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_ShouldCallDisposeAsyncCore()
    {
        // Arrange
        var service = new TestAsyncDisposableService("TestService", _loggerMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert
        service.DisposeAsyncCoreCallCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeIdempotent()
    {
        // Arrange
        var service = new TestAsyncDisposableService("TestService", _loggerMock.Object);

        // Act
        await service.DisposeAsync();
        await service.DisposeAsync();
        await service.DisposeAsync();

        // Assert
        service.DisposeAsyncCoreCallCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_WithCancellation_ShouldPassCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var service = new TestAsyncDisposableService("TestService", _loggerMock.Object);

        // Act
        await service.DisposeAsync(cts.Token);

        // Assert
        service.LastCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task DisposeAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var service = new TestAsyncDisposableService("TestService", _loggerMock.Object)
        {
            ThrowOnCancellation = true
        };

        // Act
        var act = async () => await service.DisposeAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Dispose_ShouldCallDisposeAsyncCore()
    {
        // Arrange
        var service = new TestAsyncDisposableService("TestService", _loggerMock.Object);

        // Act
        service.Dispose();

        // Assert
        service.DisposeAsyncCoreCallCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeThreadSafe()
    {
        // Arrange
        var service = new TestAsyncDisposableService("TestService", _loggerMock.Object);
        var tasks = new List<Task>();

        // Act
        for (var i = 0; i < 100; i++)
        {
            tasks.Add(service.DisposeAsync().AsTask());
        }

        await Task.WhenAll(tasks);

        // Assert
        service.DisposeAsyncCoreCallCount.Should().Be(1);
    }

    /// <summary>
    /// Test implementation of BaseAsyncDisposableService for testing purposes.
    /// </summary>
    private sealed class TestAsyncDisposableService : BaseAsyncDisposableService
    {
        public int DisposeAsyncCoreCallCount { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public bool ThrowOnCancellation { get; set; }

        public TestAsyncDisposableService(string serviceName, ILogger logger)
            : base(serviceName, logger)
        {
        }

        protected override ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
        {
            LastCancellationToken = cancellationToken;

            if (ThrowOnCancellation && cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            DisposeAsyncCoreCallCount++;
            return ValueTask.CompletedTask;
        }
    }
}
