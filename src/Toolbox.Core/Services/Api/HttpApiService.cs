// @file HttpApiService.cs
// @brief HTTP API service implementation
// @details Implements IApiService using HttpClient
// @note Supports multiple authentication modes and retry policies

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Api;

/// <summary>
/// HTTP API service implementation using HttpClient.
/// </summary>
/// <remarks>
/// <para>
/// This service provides HTTP request capabilities with support for:
/// </para>
/// <list type="bullet">
///   <item><description>All HTTP verbs</description></item>
///   <item><description>Multiple authentication modes</description></item>
///   <item><description>Automatic retry with exponential backoff</description></item>
///   <item><description>Request/response logging and telemetry</description></item>
/// </list>
/// </remarks>
/// <seealso cref="IApiService"/>
public sealed class HttpApiService : BaseAsyncDisposableService, IApiService
{
    // The HTTP client instance
    private readonly HttpClient _httpClient;

    // The service options
    private readonly ApiOptions _options;

    // The logger instance
    private readonly ILogger<HttpApiService> _logger;

    // OAuth2 token cache
    private string? _oauth2Token;
    private DateTime _oauth2TokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _oauth2Lock = new(1, 1);

    // Whether we own the HttpClient
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpApiService"/> class.
    /// </summary>
    /// <param name="options">The API service options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when options or logger is null.</exception>
    public HttpApiService(
        IOptions<ApiOptions> options,
        ILogger<HttpApiService> logger)
        : base("HttpApiService", logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
        _ownsHttpClient = true;

        _httpClient = CreateHttpClient();

        _logger.LogDebug(
            "HttpApiService initialized with base URL: {BaseUrl}, auth mode: {AuthMode}",
            _options.BaseUrl ?? "(none)",
            _options.AuthenticationMode);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpApiService"/> class with a provided HttpClient.
    /// </summary>
    /// <param name="httpClient">The HttpClient to use.</param>
    /// <param name="options">The API service options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when parameters are null.</exception>
    public HttpApiService(
        HttpClient httpClient,
        IOptions<ApiOptions> options,
        ILogger<HttpApiService> logger)
        : base("HttpApiService", logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _ownsHttpClient = false;

        ConfigureHttpClient(_httpClient);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpApiService"/> class.
    /// </summary>
    /// <param name="options">The API service options.</param>
    /// <param name="logger">The logger instance.</param>
    public HttpApiService(
        ApiOptions options,
        ILogger<HttpApiService> logger)
        : this(Microsoft.Extensions.Options.Options.Create(options), logger)
    {
    }

    /// <inheritdoc />
    public ApiResponse Send(ApiRequest request)
    {
        return SendAsync(request).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        using var activity = StartActivity();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var retryCount = 0;
        HttpResponseMessage? response = null;
        Exception? lastException = null;

        while (retryCount <= _options.MaxRetries)
        {
            try
            {
                using var httpRequest = await BuildHttpRequestAsync(request, cancellationToken);

                _logger.LogDebug(
                    "Sending {Method} request to {Url}",
                    request.Method,
                    httpRequest.RequestUri);

                response = await _httpClient.SendAsync(httpRequest, cancellationToken);

                // Don't retry on success or client errors
                if (response.IsSuccessStatusCode || (int)response.StatusCode < 500)
                {
                    break;
                }

                lastException = new HttpRequestException(
                    $"Server error: {response.StatusCode}",
                    null,
                    response.StatusCode);
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new HttpRequestException("Request timed out", ex);
            }

            retryCount++;

            if (retryCount <= _options.MaxRetries)
            {
                var delay = _options.UseExponentialBackoff
                    ? TimeSpan.FromMilliseconds(_options.RetryDelay.TotalMilliseconds * Math.Pow(2, retryCount - 1))
                    : _options.RetryDelay;

                _logger.LogWarning(
                    "Request failed, retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                    delay.TotalMilliseconds,
                    retryCount,
                    _options.MaxRetries);

                await Task.Delay(delay, cancellationToken);
            }
        }

        if (response is null)
        {
            throw lastException ?? new HttpRequestException("Request failed after all retries");
        }

        var apiResponse = await BuildApiResponseAsync(response, sw.Elapsed, cancellationToken);

        RecordOperation("Send", sw.ElapsedMilliseconds);
        RecordApiRequest(request, apiResponse);

        _logger.LogDebug(
            "Received {StatusCode} response in {Duration}ms",
            apiResponse.StatusCode,
            sw.ElapsedMilliseconds);

        return apiResponse;
    }

    /// <inheritdoc />
    public T? Send<T>(ApiRequest request)
    {
        var response = Send(request);
        response.EnsureSuccess();
        return response.Deserialize<T>();
    }

    /// <inheritdoc />
    public async Task<T?> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(request, cancellationToken);
        response.EnsureSuccess();
        return response.Deserialize<T>();
    }

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _oauth2Lock.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Creates and configures the HttpClient.
    /// </summary>
    /// <returns>The configured HttpClient.</returns>
    private HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = _options.FollowRedirects,
            MaxAutomaticRedirections = _options.MaxRedirects
        };

        // SSL certificate validation
        if (!_options.ValidateCertificate)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        // Client certificate authentication
        if (_options.AuthenticationMode == ApiAuthenticationMode.Certificate)
        {
            var certificate = GetClientCertificate();
            if (certificate is not null)
            {
                handler.ClientCertificates.Add(certificate);
            }
        }

        var client = new HttpClient(handler)
        {
            Timeout = _options.Timeout
        };

        ConfigureHttpClient(client);

        return client;
    }

    /// <summary>
    /// Configures the HttpClient with base settings.
    /// </summary>
    /// <param name="client">The HttpClient to configure.</param>
    private void ConfigureHttpClient(HttpClient client)
    {
        // Base URL
        if (!string.IsNullOrEmpty(_options.BaseUrl))
        {
            client.BaseAddress = new Uri(_options.BaseUrl);
        }

        // User-Agent
        if (!string.IsNullOrEmpty(_options.UserAgent))
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        }

        // Default headers
        foreach (var (name, value) in _options.DefaultHeaders)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
        }
    }

    /// <summary>
    /// Gets the client certificate for authentication.
    /// </summary>
    /// <returns>The X.509 certificate, or null if not configured.</returns>
    private X509Certificate2? GetClientCertificate()
    {
        if (_options.ClientCertificate is not null)
        {
            return _options.ClientCertificate;
        }

        if (!string.IsNullOrEmpty(_options.CertificatePath))
        {
            return X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes(_options.CertificatePath),
                _options.CertificatePassword,
                X509KeyStorageFlags.MachineKeySet);
        }

        return null;
    }

    /// <summary>
    /// Builds an HttpRequestMessage from an ApiRequest.
    /// </summary>
    /// <param name="request">The API request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP request message.</returns>
    private async Task<HttpRequestMessage> BuildHttpRequestAsync(
        ApiRequest request,
        CancellationToken cancellationToken)
    {
        var url = request.BuildUrl();

        // Handle API key in query string
        if (_options.AuthenticationMode == ApiAuthenticationMode.ApiKey &&
            _options.ApiKeyLocation == ApiKeyLocation.QueryString &&
            !string.IsNullOrEmpty(_options.ApiKey))
        {
            var separator = url.Contains('?') ? "&" : "?";
            url = $"{url}{separator}{Uri.EscapeDataString(_options.ApiKeyName)}={Uri.EscapeDataString(_options.ApiKey)}";
        }

        var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), url)
        {
            Content = request.Content
        };

        // Add request headers
        foreach (var (name, value) in request.Headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(name, value);
        }

        // Add authentication
        await AddAuthenticationAsync(httpRequest, cancellationToken);

        return httpRequest;
    }

    /// <summary>
    /// Adds authentication to the HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task AddAuthenticationAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        switch (_options.AuthenticationMode)
        {
            case ApiAuthenticationMode.BearerToken:
                if (!string.IsNullOrEmpty(_options.BearerToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);
                }
                break;

            case ApiAuthenticationMode.Basic:
                if (!string.IsNullOrEmpty(_options.Username))
                {
                    var credentials = Convert.ToBase64String(
                        Encoding.ASCII.GetBytes($"{_options.Username}:{_options.Password ?? string.Empty}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }
                break;

            case ApiAuthenticationMode.ApiKey:
                if (_options.ApiKeyLocation == ApiKeyLocation.Header && !string.IsNullOrEmpty(_options.ApiKey))
                {
                    request.Headers.TryAddWithoutValidation(_options.ApiKeyName, _options.ApiKey);
                }
                break;

            case ApiAuthenticationMode.OAuth2ClientCredentials:
                var token = await GetOAuth2TokenAsync(cancellationToken);
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                break;

            case ApiAuthenticationMode.Certificate:
                // Certificate is added at the handler level
                break;

            case ApiAuthenticationMode.Anonymous:
            default:
                // No authentication
                break;
        }
    }

    /// <summary>
    /// Gets an OAuth2 access token using client credentials flow.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The access token.</returns>
    private async Task<string?> GetOAuth2TokenAsync(CancellationToken cancellationToken)
    {
        // Check cached token
        if (!string.IsNullOrEmpty(_oauth2Token) && DateTime.UtcNow < _oauth2TokenExpiry)
        {
            return _oauth2Token;
        }

        await _oauth2Lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (!string.IsNullOrEmpty(_oauth2Token) && DateTime.UtcNow < _oauth2TokenExpiry)
            {
                return _oauth2Token;
            }

            if (string.IsNullOrEmpty(_options.OAuth2TokenUrl) ||
                string.IsNullOrEmpty(_options.OAuth2ClientId) ||
                string.IsNullOrEmpty(_options.OAuth2ClientSecret))
            {
                _logger.LogWarning("OAuth2 credentials not configured");
                return null;
            }

            using var tokenClient = new HttpClient();

            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.OAuth2ClientId,
                ["client_secret"] = _options.OAuth2ClientSecret
            };

            if (!string.IsNullOrEmpty(_options.OAuth2Scopes))
            {
                tokenRequest["scope"] = _options.OAuth2Scopes;
            }

            using var content = new FormUrlEncodedContent(tokenRequest);
            using var response = await tokenClient.PostAsync(_options.OAuth2TokenUrl, content, cancellationToken);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            _oauth2Token = doc.RootElement.GetProperty("access_token").GetString();

            if (doc.RootElement.TryGetProperty("expires_in", out var expiresIn))
            {
                var expiresInSeconds = expiresIn.GetInt32();
                _oauth2TokenExpiry = DateTime.UtcNow.AddSeconds(expiresInSeconds - 60); // 1 minute buffer
            }
            else
            {
                _oauth2TokenExpiry = DateTime.UtcNow.AddMinutes(55); // Default 55 minutes
            }

            _logger.LogDebug("Obtained OAuth2 token, expires at {Expiry}", _oauth2TokenExpiry);

            return _oauth2Token;
        }
        finally
        {
            _oauth2Lock.Release();
        }
    }

    /// <summary>
    /// Builds an ApiResponse from an HttpResponseMessage.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="duration">The request duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The API response.</returns>
    private static async Task<ApiResponse> BuildApiResponseAsync(
        HttpResponseMessage response,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var bodyBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        var headers = new Dictionary<string, IEnumerable<string>>();
        foreach (var header in response.Headers)
        {
            headers[header.Key] = header.Value;
        }
        foreach (var header in response.Content.Headers)
        {
            headers[header.Key] = header.Value;
        }

        return new ApiResponse
        {
            StatusCode = response.StatusCode,
            ReasonPhrase = response.ReasonPhrase,
            Body = body,
            BodyBytes = bodyBytes,
            Headers = headers,
            ContentType = response.Content.Headers.ContentType?.ToString(),
            ContentLength = response.Content.Headers.ContentLength,
            Duration = duration
        };
    }

    /// <summary>
    /// Records API request metrics.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="response">The response.</param>
    private void RecordApiRequest(ApiRequest request, ApiResponse response)
    {
        var tags = new TagList
        {
            { TelemetryConstants.Attributes.ServiceName, ServiceName },
            { "http.method", request.Method },
            { "http.status_code", response.StatusCodeValue },
            { "http.success", response.IsSuccess }
        };

        ToolboxMeter.OperationCounter.Add(1, tags);
    }
}
