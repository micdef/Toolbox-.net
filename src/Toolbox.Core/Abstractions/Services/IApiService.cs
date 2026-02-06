// @file IApiService.cs
// @brief Interface for HTTP API services
// @details Defines the contract for making HTTP requests
// @note Supports all HTTP verbs and authentication modes

using Toolbox.Core.Options;

namespace Toolbox.Core.Abstractions.Services;

/// <summary>
/// Defines the contract for HTTP API services.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides methods for making HTTP requests with support for:
/// </para>
/// <list type="bullet">
///   <item><description>All HTTP verbs (GET, POST, PUT, DELETE, PATCH, etc.)</description></item>
///   <item><description>Multiple authentication modes (Bearer, Basic, API Key, Certificate)</description></item>
///   <item><description>JSON serialization/deserialization</description></item>
///   <item><description>Custom headers and query parameters</description></item>
///   <item><description>Automatic retry with exponential backoff</description></item>
/// </list>
/// </remarks>
/// <seealso cref="IInstrumentedService"/>
/// <seealso cref="IAsyncDisposableService"/>
public interface IApiService : IInstrumentedService, IAsyncDisposableService
{
    /// <summary>
    /// Sends an HTTP request synchronously.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <returns>The API response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">Thrown when the request fails.</exception>
    /// <example>
    /// <code>
    /// var request = ApiRequest.Get("/users/123");
    /// var response = apiService.Send(request);
    ///
    /// if (response.IsSuccess)
    /// {
    ///     var user = response.Deserialize&lt;User&gt;();
    /// }
    /// </code>
    /// </example>
    ApiResponse Send(ApiRequest request);

    /// <summary>
    /// Sends an HTTP request asynchronously.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the API response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">Thrown when the request fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <example>
    /// <code>
    /// var request = ApiRequest.Post("/users", new { Name = "John" });
    /// var response = await apiService.SendAsync(request);
    ///
    /// response.EnsureSuccess();
    /// var createdUser = response.Deserialize&lt;User&gt;();
    /// </code>
    /// </example>
    Task<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an HTTP request and deserializes the response.
    /// </summary>
    /// <typeparam name="T">The response body type.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <returns>The deserialized response body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">Thrown when the request fails or returns an error status.</exception>
    /// <example>
    /// <code>
    /// var users = apiService.Send&lt;List&lt;User&gt;&gt;(ApiRequest.Get("/users"));
    /// </code>
    /// </example>
    T? Send<T>(ApiRequest request);

    /// <summary>
    /// Sends an HTTP request and deserializes the response asynchronously.
    /// </summary>
    /// <typeparam name="T">The response body type.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the deserialized response body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">Thrown when the request fails or returns an error status.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <example>
    /// <code>
    /// var user = await apiService.SendAsync&lt;User&gt;(
    ///     ApiRequest.Get("/users/123"),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<T?> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default);
}
