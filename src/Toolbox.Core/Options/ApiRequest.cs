// @file ApiRequest.cs
// @brief API request representation
// @details Contains all data needed to make an HTTP request
// @note Supports all HTTP methods and content types

using System.Text;
using System.Text.Json;

namespace Toolbox.Core.Options;

/// <summary>
/// Represents an HTTP API request.
/// </summary>
/// <remarks>
/// <para>
/// This class encapsulates all the data needed to make an HTTP request,
/// including URL, method, headers, and body content.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // GET request
/// var request = new ApiRequest("GET", "/users");
///
/// // POST with JSON body
/// var request = ApiRequest.Post("/users", new { Name = "John" });
///
/// // With custom headers
/// var request = new ApiRequest("GET", "/data")
///     .WithHeader("X-Custom", "value");
/// </code>
/// </example>
public sealed class ApiRequest
{
    /// <summary>
    /// Gets the HTTP method.
    /// </summary>
    /// <value>The HTTP method (GET, POST, PUT, DELETE, etc.).</value>
    public string Method { get; }

    /// <summary>
    /// Gets the request URL or path.
    /// </summary>
    /// <value>The URL or path. Can be relative if base URL is configured.</value>
    public string Url { get; }

    /// <summary>
    /// Gets or sets the request body content.
    /// </summary>
    /// <value>The body content, or <c>null</c> for no body.</value>
    public HttpContent? Content { get; set; }

    /// <summary>
    /// Gets the request headers.
    /// </summary>
    /// <value>Dictionary of header names and values.</value>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the query parameters.
    /// </summary>
    /// <value>Dictionary of query parameter names and values.</value>
    public IDictionary<string, string> QueryParameters { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the request timeout override.
    /// </summary>
    /// <value>Custom timeout, or <c>null</c> to use default.</value>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiRequest"/> class.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">The URL or path.</param>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public ApiRequest(string method, string url)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(url);

        Method = method.ToUpperInvariant();
        Url = url;
    }

    /// <summary>
    /// Creates a GET request.
    /// </summary>
    /// <param name="url">The URL or path.</param>
    /// <returns>A new <see cref="ApiRequest"/>.</returns>
    public static ApiRequest Get(string url) => new("GET", url);

    /// <summary>
    /// Creates a POST request with JSON content.
    /// </summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="url">The URL or path.</param>
    /// <param name="body">The request body.</param>
    /// <param name="options">JSON serializer options.</param>
    /// <returns>A new <see cref="ApiRequest"/>.</returns>
    public static ApiRequest Post<T>(string url, T body, JsonSerializerOptions? options = null)
    {
        var request = new ApiRequest("POST", url);
        request.SetJsonContent(body, options);
        return request;
    }

    /// <summary>
    /// Creates a POST request.
    /// </summary>
    /// <param name="url">The URL or path.</param>
    /// <returns>A new <see cref="ApiRequest"/>.</returns>
    public static ApiRequest Post(string url) => new("POST", url);

    /// <summary>
    /// Creates a PUT request with JSON content.
    /// </summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="url">The URL or path.</param>
    /// <param name="body">The request body.</param>
    /// <param name="options">JSON serializer options.</param>
    /// <returns>A new <see cref="ApiRequest"/>.</returns>
    public static ApiRequest Put<T>(string url, T body, JsonSerializerOptions? options = null)
    {
        var request = new ApiRequest("PUT", url);
        request.SetJsonContent(body, options);
        return request;
    }

    /// <summary>
    /// Creates a PUT request.
    /// </summary>
    /// <param name="url">The URL or path.</param>
    /// <returns>A new <see cref="ApiRequest"/>.</returns>
    public static ApiRequest Put(string url) => new("PUT", url);

    /// <summary>
    /// Creates a PATCH request with JSON content.
    /// </summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="url">The URL or path.</param>
    /// <param name="body">The request body.</param>
    /// <param name="options">JSON serializer options.</param>
    /// <returns>A new <see cref="ApiRequest"/>.</returns>
    public static ApiRequest Patch<T>(string url, T body, JsonSerializerOptions? options = null)
    {
        var request = new ApiRequest("PATCH", url);
        request.SetJsonContent(body, options);
        return request;
    }

    /// <summary>
    /// Creates a PATCH request.
    /// </summary>
    /// <param name="url">The URL or path.</param>
    /// <returns>A new <see cref="ApiRequest"/>.</returns>
    public static ApiRequest Patch(string url) => new("PATCH", url);

    /// <summary>
    /// Creates a DELETE request.
    /// </summary>
    /// <param name="url">The URL or path.</param>
    /// <returns>A new <see cref="ApiRequest"/>.</returns>
    public static ApiRequest Delete(string url) => new("DELETE", url);

    /// <summary>
    /// Creates a HEAD request.
    /// </summary>
    /// <param name="url">The URL or path.</param>
    /// <returns>A new <see cref="ApiRequest"/>.</returns>
    public static ApiRequest Head(string url) => new("HEAD", url);

    /// <summary>
    /// Creates an OPTIONS request.
    /// </summary>
    /// <param name="url">The URL or path.</param>
    /// <returns>A new <see cref="ApiRequest"/>.</returns>
    public static ApiRequest Options(string url) => new("OPTIONS", url);

    /// <summary>
    /// Adds a header to the request.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>This request for chaining.</returns>
    public ApiRequest WithHeader(string name, string value)
    {
        Headers[name] = value;
        return this;
    }

    /// <summary>
    /// Adds a query parameter to the request.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>This request for chaining.</returns>
    public ApiRequest WithQuery(string name, string value)
    {
        QueryParameters[name] = value;
        return this;
    }

    /// <summary>
    /// Sets a custom timeout for this request.
    /// </summary>
    /// <param name="timeout">The timeout.</param>
    /// <returns>This request for chaining.</returns>
    public ApiRequest WithTimeout(TimeSpan timeout)
    {
        Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets JSON content for the request body.
    /// </summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="body">The request body.</param>
    /// <param name="options">JSON serializer options.</param>
    /// <returns>This request for chaining.</returns>
    public ApiRequest SetJsonContent<T>(T body, JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(body, options);
        Content = new StringContent(json, Encoding.UTF8, "application/json");
        return this;
    }

    /// <summary>
    /// Sets string content for the request body.
    /// </summary>
    /// <param name="content">The content string.</param>
    /// <param name="contentType">The content type. Default is "text/plain".</param>
    /// <returns>This request for chaining.</returns>
    public ApiRequest SetStringContent(string content, string contentType = "text/plain")
    {
        Content = new StringContent(content, Encoding.UTF8, contentType);
        return this;
    }

    /// <summary>
    /// Sets form URL-encoded content for the request body.
    /// </summary>
    /// <param name="formData">The form data.</param>
    /// <returns>This request for chaining.</returns>
    public ApiRequest SetFormContent(IEnumerable<KeyValuePair<string, string>> formData)
    {
        Content = new FormUrlEncodedContent(formData);
        return this;
    }

    /// <summary>
    /// Sets binary content for the request body.
    /// </summary>
    /// <param name="bytes">The byte array.</param>
    /// <param name="contentType">The content type. Default is "application/octet-stream".</param>
    /// <returns>This request for chaining.</returns>
    public ApiRequest SetBinaryContent(byte[] bytes, string contentType = "application/octet-stream")
    {
        Content = new ByteArrayContent(bytes);
        Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return this;
    }

    /// <summary>
    /// Sets stream content for the request body.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="contentType">The content type. Default is "application/octet-stream".</param>
    /// <returns>This request for chaining.</returns>
    public ApiRequest SetStreamContent(Stream stream, string contentType = "application/octet-stream")
    {
        Content = new StreamContent(stream);
        Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return this;
    }

    /// <summary>
    /// Builds the full URL with query parameters.
    /// </summary>
    /// <returns>The URL with query string.</returns>
    public string BuildUrl()
    {
        if (QueryParameters.Count == 0)
        {
            return Url;
        }

        var separator = Url.Contains('?') ? "&" : "?";
        var queryString = string.Join("&",
            QueryParameters.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{Url}{separator}{queryString}";
    }
}
