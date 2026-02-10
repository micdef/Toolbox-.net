// @file TokenRefreshService.cs
// @brief Background service for automatic token refresh
// @details Monitors registered sessions and refreshes tokens before expiration

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Sso;

/// <summary>
/// Background service that monitors and automatically refreshes tokens before expiration.
/// </summary>
/// <remarks>
/// <para>
/// This service runs as a hosted background service and periodically checks all registered
/// sessions for token expiration. When a session's tokens are about to expire (based on
/// the configured threshold), the service triggers a refresh.
/// </para>
/// <para>
/// The service uses retry logic with exponential backoff for failed refresh attempts.
/// </para>
/// </remarks>
public sealed class TokenRefreshService : BaseAsyncDisposableService, ITokenRefreshService, IHostedService
{
    #region Fields

    /// <summary>
    /// The activity source for distributed tracing.
    /// </summary>
    private static new readonly ActivitySource ActivitySource = new(
        TelemetryConstants.ActivitySourceName,
        TelemetryConstants.ActivitySourceVersion);

    /// <summary>
    /// Registered sessions for refresh monitoring.
    /// </summary>
    private readonly ConcurrentDictionary<string, SessionRefreshInfo> _registeredSessions = new();

    /// <summary>
    /// The SSO session manager for refreshing sessions.
    /// </summary>
    private readonly ISsoSessionManager? _sessionManager;

    /// <summary>
    /// The SSO session options.
    /// </summary>
    private readonly SsoSessionOptions _options;

    /// <summary>
    /// The logger instance.
    /// </summary>
    private readonly ILogger<TokenRefreshService> _logger;

    /// <summary>
    /// Timer for periodic refresh checks.
    /// </summary>
    private Timer? _refreshTimer;

    /// <summary>
    /// Cancellation token source for stopping the service.
    /// </summary>
    private CancellationTokenSource? _stoppingCts;

    /// <summary>
    /// Whether the service is currently running.
    /// </summary>
    private volatile bool _isRunning;

    /// <summary>
    /// Lock for service state changes.
    /// </summary>
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    /// <summary>
    /// Counter for successful refreshes.
    /// </summary>
    private long _successfulRefreshCount;

    /// <summary>
    /// Counter for failed refreshes.
    /// </summary>
    private long _failedRefreshCount;

    /// <summary>
    /// Time of last refresh check.
    /// </summary>
    private DateTimeOffset? _lastCheckTime;

    /// <summary>
    /// Maximum retry attempts for failed refreshes.
    /// </summary>
    private const int MaxRetryAttempts = 3;

    /// <summary>
    /// Base delay for exponential backoff.
    /// </summary>
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(5);

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenRefreshService"/> class.
    /// </summary>
    /// <param name="options">The SSO session options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="sessionManager">Optional session manager for performing refreshes.</param>
    public TokenRefreshService(
        IOptions<SsoSessionOptions> options,
        ILogger<TokenRefreshService> logger,
        ISsoSessionManager? sessionManager = null)
        : base(nameof(TokenRefreshService), logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
        _sessionManager = sessionManager;

        _logger.LogInformation(
            "TokenRefreshService initialized with check interval {Interval}, threshold {Threshold}",
            _options.RefreshCheckInterval,
            _options.RefreshThreshold);
    }

    #endregion

    #region IHostedService

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isRunning)
            {
                _logger.LogDebug("TokenRefreshService is already running");
                return;
            }

            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _refreshTimer = new Timer(
                async _ => await CheckAndRefreshAsync().ConfigureAwait(false),
                null,
                _options.RefreshCheckInterval,
                _options.RefreshCheckInterval);

            _isRunning = true;
            _logger.LogInformation("TokenRefreshService started");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isRunning)
            {
                return;
            }

            _stoppingCts?.Cancel();

            if (_refreshTimer != null)
            {
                await _refreshTimer.DisposeAsync().ConfigureAwait(false);
                _refreshTimer = null;
            }

            _isRunning = false;
            _logger.LogInformation("TokenRefreshService stopped");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    #endregion

    #region ITokenRefreshService - Registration

    /// <inheritdoc />
    public Task RegisterForRefreshAsync(
        SsoSession session,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrEmpty(session.RefreshToken))
        {
            _logger.LogDebug(
                "Session {SessionId} has no refresh token, skipping registration",
                session.SessionId);
            return Task.CompletedTask;
        }

        var info = new SessionRefreshInfo
        {
            Session = session,
            RegisteredAt = DateTimeOffset.UtcNow,
            LastRefreshAttempt = null,
            RetryCount = 0
        };

        _registeredSessions[session.SessionId] = info;

        _logger.LogDebug(
            "Registered session {SessionId} for refresh, expires at {ExpiresAt}",
            session.SessionId,
            session.ExpiresAt);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> UnregisterFromRefreshAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var removed = _registeredSessions.TryRemove(sessionId, out _);

        if (removed)
        {
            _logger.LogDebug("Unregistered session {SessionId} from refresh", sessionId);
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task UpdateRegistrationAsync(
        SsoSession session,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(session);

        if (_registeredSessions.TryGetValue(session.SessionId, out var info))
        {
            info.Session = session;
            info.RetryCount = 0;
            info.LastRefreshAttempt = DateTimeOffset.UtcNow;

            _logger.LogDebug(
                "Updated registration for session {SessionId}, new expiration {ExpiresAt}",
                session.SessionId,
                session.ExpiresAt);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public bool IsRegistered(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        return _registeredSessions.ContainsKey(sessionId);
    }

    #endregion

    #region ITokenRefreshService - Properties

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public int RegisteredSessionCount => _registeredSessions.Count;

    /// <inheritdoc />
    public long SuccessfulRefreshCount => Interlocked.Read(ref _successfulRefreshCount);

    /// <inheritdoc />
    public long FailedRefreshCount => Interlocked.Read(ref _failedRefreshCount);

    /// <inheritdoc />
    public DateTimeOffset? LastCheckTime => _lastCheckTime;

    /// <inheritdoc />
    public DateTimeOffset? NextCheckTime => _isRunning && _lastCheckTime.HasValue
        ? _lastCheckTime.Value.Add(_options.RefreshCheckInterval)
        : null;

    #endregion

    #region ITokenRefreshService - Manual Refresh

    /// <inheritdoc />
    public async Task<SsoSession?> RefreshNowAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        if (!_registeredSessions.TryGetValue(sessionId, out var info))
        {
            _logger.LogWarning("Session {SessionId} is not registered for refresh", sessionId);
            return null;
        }

        return await RefreshSessionAsync(info, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> RefreshAllPendingAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var refreshed = 0;
        var sessionsNeedingRefresh = _registeredSessions.Values
            .Where(info => info.Session.NeedsRefresh(_options.RefreshThreshold))
            .ToList();

        foreach (var info in sessionsNeedingRefresh)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var result = await RefreshSessionAsync(info, cancellationToken).ConfigureAwait(false);
            if (result != null)
            {
                refreshed++;
            }
        }

        return refreshed;
    }

    #endregion

    #region ITokenRefreshService - Events

    /// <inheritdoc />
    public event EventHandler<TokenRefreshNeededEventArgs>? RefreshNeeded;

    /// <inheritdoc />
    public event EventHandler<TokenRefreshCompletedEventArgs>? RefreshCompleted;

    /// <inheritdoc />
    public event EventHandler<TokenRefreshFailedEventArgs>? RefreshFailed;

    /// <summary>
    /// Raises the <see cref="RefreshNeeded"/> event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnRefreshNeeded(TokenRefreshNeededEventArgs e) => RefreshNeeded?.Invoke(this, e);

    /// <summary>
    /// Raises the <see cref="RefreshCompleted"/> event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnRefreshCompleted(TokenRefreshCompletedEventArgs e) => RefreshCompleted?.Invoke(this, e);

    /// <summary>
    /// Raises the <see cref="RefreshFailed"/> event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnRefreshFailed(TokenRefreshFailedEventArgs e) => RefreshFailed?.Invoke(this, e);

    #endregion

    #region Private Methods

    /// <summary>
    /// Periodic callback to check and refresh sessions.
    /// </summary>
    private async Task CheckAndRefreshAsync()
    {
        if (IsDisposed || !_isRunning)
        {
            return;
        }

        using var activity = ActivitySource.StartActivity("TokenRefreshService.CheckAndRefresh");
        _lastCheckTime = DateTimeOffset.UtcNow;

        var sessionsNeedingRefresh = _registeredSessions.Values
            .Where(info => info.Session.NeedsRefresh(_options.RefreshThreshold))
            .ToList();

        if (sessionsNeedingRefresh.Count == 0)
        {
            return;
        }

        _logger.LogDebug(
            "Found {Count} sessions needing refresh out of {Total} registered",
            sessionsNeedingRefresh.Count,
            _registeredSessions.Count);

        foreach (var info in sessionsNeedingRefresh)
        {
            if (IsDisposed || !_isRunning || _stoppingCts?.IsCancellationRequested == true)
            {
                break;
            }

            // Raise RefreshNeeded event
            var timeToExpiry = info.Session.ExpiresAt - DateTimeOffset.UtcNow;
            var lifetime = info.Session.ExpiresAt - info.Session.CreatedAt;
            var elapsedPercent = lifetime.TotalSeconds > 0
                ? 1.0 - (timeToExpiry.TotalSeconds / lifetime.TotalSeconds)
                : 1.0;

            OnRefreshNeeded(new TokenRefreshNeededEventArgs
            {
                Session = info.Session,
                LifetimeElapsedPercent = elapsedPercent,
                TimeToExpiry = timeToExpiry
            });

            await RefreshSessionAsync(info, _stoppingCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        }

        activity?.SetTag("sessions.checked", _registeredSessions.Count);
        activity?.SetTag("sessions.refreshed", sessionsNeedingRefresh.Count);
    }

    /// <summary>
    /// Refreshes a single session.
    /// </summary>
    /// <param name="info">The session refresh info.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The refreshed session, or null if refresh failed.</returns>
    private async Task<SsoSession?> RefreshSessionAsync(
        SessionRefreshInfo info,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        info.LastRefreshAttempt = DateTimeOffset.UtcNow;

        try
        {
            if (_sessionManager == null)
            {
                _logger.LogWarning(
                    "Cannot refresh session {SessionId}: no session manager available",
                    info.Session.SessionId);
                return null;
            }

            var refreshedSession = await _sessionManager.RefreshSessionAsync(
                info.Session.SessionId,
                cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            info.Session = refreshedSession;
            info.RetryCount = 0;

            Interlocked.Increment(ref _successfulRefreshCount);

            OnRefreshCompleted(new TokenRefreshCompletedEventArgs
            {
                SessionId = refreshedSession.SessionId,
                NewAccessToken = refreshedSession.AccessToken,
                NewExpiresAt = refreshedSession.ExpiresAt,
                RefreshDuration = stopwatch.Elapsed
            });

            _logger.LogDebug(
                "Successfully refreshed session {SessionId} in {Duration}ms",
                info.Session.SessionId,
                stopwatch.ElapsedMilliseconds);

            return refreshedSession;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            info.RetryCount++;
            var willRetry = info.RetryCount < MaxRetryAttempts;

            Interlocked.Increment(ref _failedRefreshCount);

            OnRefreshFailed(new TokenRefreshFailedEventArgs
            {
                SessionId = info.Session.SessionId,
                Exception = ex,
                WillRetry = willRetry,
                RetryAttempt = info.RetryCount,
                MaxRetries = MaxRetryAttempts
            });

            _logger.LogWarning(
                ex,
                "Failed to refresh session {SessionId} (attempt {Attempt}/{Max})",
                info.Session.SessionId,
                info.RetryCount,
                MaxRetryAttempts);

            if (willRetry)
            {
                // Schedule retry with exponential backoff
                var delay = TimeSpan.FromSeconds(BaseRetryDelay.TotalSeconds * Math.Pow(2, info.RetryCount - 1));
                _ = Task.Delay(delay, cancellationToken)
                    .ContinueWith(
                        async _ => await RefreshSessionAsync(info, cancellationToken).ConfigureAwait(false),
                        cancellationToken,
                        TaskContinuationOptions.OnlyOnRanToCompletion,
                        TaskScheduler.Default);
            }
            else
            {
                // Max retries exceeded, unregister
                _registeredSessions.TryRemove(info.Session.SessionId, out _);
                _logger.LogError(
                    "Max refresh retries exceeded for session {SessionId}, unregistering",
                    info.Session.SessionId);
            }

            return null;
        }
    }

    #endregion

    #region Disposal

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);

        _stoppingCts?.Dispose();
        _stoppingCts = null;

        _stateLock.Dispose();
        _registeredSessions.Clear();

        _logger.LogInformation("TokenRefreshService disposed");
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// Information about a registered session for refresh monitoring.
    /// </summary>
    private sealed class SessionRefreshInfo
    {
        /// <summary>
        /// Gets or sets the session.
        /// </summary>
        public SsoSession Session { get; set; } = null!;

        /// <summary>
        /// Gets or sets when the session was registered.
        /// </summary>
        public DateTimeOffset RegisteredAt { get; set; }

        /// <summary>
        /// Gets or sets when the last refresh was attempted.
        /// </summary>
        public DateTimeOffset? LastRefreshAttempt { get; set; }

        /// <summary>
        /// Gets or sets the current retry count.
        /// </summary>
        public int RetryCount { get; set; }
    }

    #endregion
}
