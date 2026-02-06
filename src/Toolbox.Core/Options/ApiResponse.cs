// @file ApiResponse.cs
// @brief API response representation
// @details Contains HTTP response data including status, headers, and body
// @note Provides convenience methods for deserializing JSON responses

using System.Net;
using System.Text.Json;

namespace Toolbox.Core.Options;

/// <summary>
/// Represents an HTTP API response.
/// </summary>
/// <remarks>
/// <para>
/// This class encapsulates all data from an HTTP response,
/// including status code, headers, and body content.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var response = await apiService.SendAsync(request);
///
/// if (response.IsSuccess)
/// {
///     var user = response.Deserialize&lt;User&gt;();
/// }
/// else
/// {
///     Console.WriteLine($"Error: {response.StatusCode}");
/// }
/// </code>
/// </example>
public sealed class ApiResponse
{
    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    /// <value>The status code.</value>
    public HttpStatusCode StatusCode { get; init; }

    /// <summary>
    /// Gets the status code as an integer.
    /// </summary>
    /// <value>The numeric status code.</value>
    public int StatusCodeValue => (int)StatusCode;

    /// <summary>
    /// Gets the reason phrase.
    /// </summary>
    /// <value>The reason phrase (e.g., "OK", "Not Found").</value>
    public string? ReasonPhrase { get; init; }

    /// <summary>
    /// Gets the response headers.
    /// </summary>
    /// <value>Dictionary of header names and values.</value>
    public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; init; }
        = new Dictionary<string, IEnumerable<string>>();

    /// <summary>
    /// Gets the response body as a string.
    /// </summary>
    /// <value>The body content, or <c>null</c> if no body.</value>
    public string? Body { get; init; }

    /// <summary>
    /// Gets the response body as bytes.
    /// </summary>
    /// <value>The body bytes, or <c>null</c> if no body.</value>
    public byte[]? BodyBytes { get; init; }

    /// <summary>
    /// Gets the content type header value.
    /// </summary>
    /// <value>The content type, or <c>null</c> if not set.</value>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the content length.
    /// </summary>
    /// <value>The content length in bytes, or <c>null</c> if not set.</value>
    public long? ContentLength { get; init; }

    /// <summary>
    /// Gets the request duration.
    /// </summary>
    /// <value>The time taken for the request.</value>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets a value indicating whether the response indicates success (2xx status).
    /// </summary>
    /// <value><c>true</c> if status code is 2xx; otherwise, <c>false</c>.</value>
    public bool IsSuccess => StatusCodeValue >= 200 && StatusCodeValue < 300;

    /// <summary>
    /// Gets a value indicating whether the response indicates a client error (4xx status).
    /// </summary>
    /// <value><c>true</c> if status code is 4xx; otherwise, <c>false</c>.</value>
    public bool IsClientError => StatusCodeValue >= 400 && StatusCodeValue < 500;

    /// <summary>
    /// Gets a value indicating whether the response indicates a server error (5xx status).
    /// </summary>
    /// <value><c>true</c> if status code is 5xx; otherwise, <c>false</c>.</value>
    public bool IsServerError => StatusCodeValue >= 500 && StatusCodeValue < 600;

    /// <summary>
    /// Gets a value indicating whether the response is a redirect (3xx status).
    /// </summary>
    /// <value><c>true</c> if status code is 3xx; otherwise, <c>false</c>.</value>
    public bool IsRedirect => StatusCodeValue >= 300 && StatusCodeValue < 400;

    /// <summary>
    /// Deserializes the response body as JSON.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="options">JSON serializer options.</param>
    /// <returns>The deserialized object, or default if body is empty.</returns>
    /// <exception cref="JsonException">Thrown when deserialization fails.</exception>
    public T? Deserialize<T>(JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrEmpty(Body))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(Body, options);
    }

    /// <summary>
    /// Tries to deserialize the response body as JSON.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="result">The deserialized object if successful.</param>
    /// <param name="options">JSON serializer options.</param>
    /// <returns><c>true</c> if deserialization succeeded; otherwise, <c>false</c>.</returns>
    public bool TryDeserialize<T>(out T? result, JsonSerializerOptions? options = null)
    {
        result = default;

        if (string.IsNullOrEmpty(Body))
        {
            return false;
        }

        try
        {
            result = JsonSerializer.Deserialize<T>(Body, options);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets a header value.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <returns>The first header value, or <c>null</c> if not found.</returns>
    public string? GetHeader(string name)
    {
        return Headers.TryGetValue(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    /// <summary>
    /// Gets all values for a header.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <returns>All header values, or empty if not found.</returns>
    public IEnumerable<string> GetHeaders(string name)
    {
        return Headers.TryGetValue(name, out var values)
            ? values
            : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Throws an exception if the response indicates an error.
    /// </summary>
    /// <exception cref="HttpRequestException">Thrown when response is not successful.</exception>
    public void EnsureSuccess()
    {
        if (!IsSuccess)
        {
            throw new HttpRequestException(
                $"HTTP request failed with status {StatusCodeValue} ({ReasonPhrase}): {Body}",
                null,
                StatusCode);
        }
    }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    /// <param name="body">The response body.</param>
    /// <param name="statusCode">The status code.</param>
    /// <returns>A new <see cref="ApiResponse"/>.</returns>
    public static ApiResponse Success(string? body = null, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new ApiResponse
        {
            StatusCode = statusCode,
            ReasonPhrase = statusCode.ToString(),
            Body = body,
            Duration = TimeSpan.Zero
        };
    }

    /// <summary>
    /// Creates an error response.
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A new <see cref="ApiResponse"/>.</returns>
    public static ApiResponse Error(HttpStatusCode statusCode, string? message = null)
    {
        return new ApiResponse
        {
            StatusCode = statusCode,
            ReasonPhrase = statusCode.ToString(),
            Body = message,
            Duration = TimeSpan.Zero
        };
    }
}
