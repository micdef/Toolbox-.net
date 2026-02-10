// @file DeviceCodeInfo.cs
// @brief DTO for Azure AD device code flow information
// @details Contains device code, verification URI, and user message for OAuth2 device flow
// @note Used by AzureAdService.AuthenticateWithDeviceCodeAsync

namespace Toolbox.Core.Options;

/// <summary>
/// Contains information for the OAuth2 device code flow.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the information needed to complete the device code
/// authentication flow. The user must:
/// </para>
/// <list type="number">
///   <item><description>Navigate to <see cref="VerificationUri"/></description></item>
///   <item><description>Enter the <see cref="UserCode"/></description></item>
///   <item><description>Complete authentication in their browser</description></item>
/// </list>
/// <para>
/// This flow is useful for devices without a keyboard or where browser
/// authentication is not possible (e.g., CLI tools, IoT devices).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var result = await azureAdService.AuthenticateWithDeviceCodeAsync(async info =>
/// {
///     Console.WriteLine(info.Message);
///     // User navigates to URL and enters code
///     await Task.CompletedTask;
/// });
/// </code>
/// </example>
public sealed class DeviceCodeInfo
{
    /// <summary>
    /// Gets or initializes the user code to be entered.
    /// </summary>
    /// <value>
    /// A short alphanumeric code (e.g., "ABCD-1234") that the user
    /// must enter at the verification URL.
    /// </value>
    public required string UserCode { get; init; }

    /// <summary>
    /// Gets or initializes the verification URI.
    /// </summary>
    /// <value>
    /// The URL where the user should navigate to enter the code
    /// (typically https://microsoft.com/devicelogin).
    /// </value>
    public required string VerificationUri { get; init; }

    /// <summary>
    /// Gets or initializes the complete verification URI including the code.
    /// </summary>
    /// <value>
    /// The URL with the code pre-filled, for easier user experience.
    /// May be <c>null</c> if not provided by the identity provider.
    /// </value>
    public string? VerificationUriComplete { get; init; }

    /// <summary>
    /// Gets or initializes the user-friendly message.
    /// </summary>
    /// <value>
    /// A message that can be displayed to the user with instructions
    /// for completing the authentication flow.
    /// </value>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or initializes when the device code expires.
    /// </summary>
    /// <value>
    /// The UTC timestamp after which the device code is no longer valid.
    /// </value>
    public DateTimeOffset ExpiresOn { get; init; }

    /// <summary>
    /// Gets or initializes the interval between polling attempts.
    /// </summary>
    /// <value>
    /// The minimum interval (in seconds) between token polling requests.
    /// Typically 5 seconds.
    /// </value>
    public int Interval { get; init; } = 5;

    /// <summary>
    /// Gets or initializes the client ID used for the request.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets or initializes the requested scopes.
    /// </summary>
    public IReadOnlyList<string>? Scopes { get; init; }
}
