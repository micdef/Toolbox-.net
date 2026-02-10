// @file InMemoryCredentialStore.cs
// @brief In-memory credential store for testing and development
// @details Non-persistent credential storage using ConcurrentDictionary

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Sso;

/// <summary>
/// In-memory credential store for testing and development scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores credentials only in memory and does not persist
/// them. All credentials are lost when the application restarts.
/// </para>
/// <para>
/// This store is thread-safe and suitable for unit tests and development.
/// </para>
/// </remarks>
public sealed class InMemoryCredentialStore : BaseAsyncDisposableService, ICredentialStore
{
    #region Fields

    /// <summary>
    /// The activity source for distributed tracing.
    /// </summary>
    private static new readonly ActivitySource ActivitySource = new(
        TelemetryConstants.ActivitySourceName,
        TelemetryConstants.ActivitySourceVersion);

    /// <summary>
    /// The logger instance.
    /// </summary>
    private readonly ILogger<InMemoryCredentialStore> _logger;

    /// <summary>
    /// In-memory storage for credentials.
    /// </summary>
    private readonly ConcurrentDictionary<string, SsoCredential> _credentials = new();

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryCredentialStore"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public InMemoryCredentialStore(ILogger<InMemoryCredentialStore> logger)
        : base(nameof(InMemoryCredentialStore), logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;

        _logger.LogInformation("InMemoryCredentialStore initialized (non-persistent)");
    }

    #endregion

    #region ICredentialStore - Store Operations

    /// <inheritdoc />
    public Task StoreCredentialAsync(
        string key,
        SsoCredential credential,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(credential);

        using var activity = ActivitySource.StartActivity("InMemoryCredentialStore.StoreCredential");
        activity?.SetTag("credential.key", key);

        _credentials[key] = credential;

        _logger.LogDebug("Stored credential for key {Key}", key);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SsoCredential?> GetCredentialAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var activity = ActivitySource.StartActivity("InMemoryCredentialStore.GetCredential");
        activity?.SetTag("credential.key", key);

        _credentials.TryGetValue(key, out var credential);

        if (credential != null)
        {
            _logger.LogDebug("Retrieved credential for key {Key}", key);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        else
        {
            _logger.LogDebug("Credential not found for key {Key}", key);
        }

        return Task.FromResult(credential);
    }

    /// <inheritdoc />
    public Task<bool> RemoveCredentialAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var activity = ActivitySource.StartActivity("InMemoryCredentialStore.RemoveCredential");
        activity?.SetTag("credential.key", key);

        var removed = _credentials.TryRemove(key, out _);

        if (removed)
        {
            _logger.LogDebug("Removed credential for key {Key}", key);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<SsoCredential> UpdateCredentialAsync(
        string key,
        Func<SsoCredential?, SsoCredential> updateFunc,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(updateFunc);

        _credentials.TryGetValue(key, out var existing);
        var updated = updateFunc(existing);
        _credentials[key] = updated;

        return Task.FromResult(updated);
    }

    #endregion

    #region ICredentialStore - Query Operations

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);

        return Task.FromResult(_credentials.ContainsKey(key));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListKeysAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var keys = _credentials
            .Where(kvp => string.Equals(kvp.Value.UserId, userId, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListKeysByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        IEnumerable<string> keys;

        if (pattern == "*")
        {
            keys = _credentials.Keys;
        }
        else if (pattern.EndsWith('*'))
        {
            var prefix = pattern[..^1];
            keys = _credentials.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
        else if (pattern.StartsWith('*'))
        {
            var suffix = pattern[1..];
            keys = _credentials.Keys.Where(k => k.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            keys = _credentials.Keys.Where(k => k.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<string>>(keys.ToList());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SsoCredential>> GetUserCredentialsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var credentials = _credentials.Values
            .Where(c => string.Equals(c.UserId, userId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IReadOnlyList<SsoCredential>>(credentials);
    }

    #endregion

    #region ICredentialStore - Maintenance Operations

    /// <inheritdoc />
    public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _credentials
            .Where(kvp => kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _credentials.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired credentials", expiredKeys.Count);
        }

        return Task.FromResult(expiredKeys.Count);
    }

    /// <inheritdoc />
    public Task<int> ClearUserCredentialsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var userKeys = _credentials
            .Where(kvp => string.Equals(kvp.Value.UserId, userId, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in userKeys)
        {
            _credentials.TryRemove(key, out _);
        }

        if (userKeys.Count > 0)
        {
            _logger.LogInformation("Cleared {Count} credentials for user {UserId}", userKeys.Count, userId);
        }

        return Task.FromResult(userKeys.Count);
    }

    /// <inheritdoc />
    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var count = _credentials.Count;
        _credentials.Clear();

        _logger.LogWarning("Cleared all {Count} credentials", count);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> GetCredentialCountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.FromResult(_credentials.Count);
    }

    #endregion

    #region Disposal

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        _credentials.Clear();
        _logger.LogInformation("InMemoryCredentialStore disposed");
        return ValueTask.CompletedTask;
    }

    #endregion
}
