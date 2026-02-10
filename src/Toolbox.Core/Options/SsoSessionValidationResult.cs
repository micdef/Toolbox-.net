// @file SsoSessionValidationResult.cs
// @brief Result of SSO session validation
// @details Contains validation status, session data, and failure information

namespace Toolbox.Core.Options;

/// <summary>
/// Represents the result of validating an SSO session.
/// </summary>
/// <remarks>
/// <para>
/// This class provides detailed information about session validation,
/// including the validated session (if successful) and failure reasons.
/// </para>
/// </remarks>
public sealed class SsoSessionValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the session is valid.
    /// </summary>
    /// <value><c>true</c> if the session is valid; otherwise, <c>false</c>.</value>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validated session.
    /// </summary>
    /// <value>The session if valid, or null if validation failed.</value>
    public SsoSession? Session { get; init; }

    /// <summary>
    /// Gets the reason for validation failure.
    /// </summary>
    /// <value>The failure reason, or null if validation succeeded.</value>
    public SsoValidationFailureReason? FailureReason { get; init; }

    /// <summary>
    /// Gets a human-readable error message.
    /// </summary>
    /// <value>The error message, or null if validation succeeded.</value>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the time when validation was performed.
    /// </summary>
    public DateTimeOffset ValidatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="session">The validated session.</param>
    /// <returns>A new <see cref="SsoSessionValidationResult"/> indicating success.</returns>
    public static SsoSessionValidationResult Success(SsoSession session) => new()
    {
        IsValid = true,
        Session = session
    };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="reason">The reason for failure.</param>
    /// <param name="message">An optional error message.</param>
    /// <returns>A new <see cref="SsoSessionValidationResult"/> indicating failure.</returns>
    public static SsoSessionValidationResult Failed(
        SsoValidationFailureReason reason,
        string? message = null) => new()
    {
        IsValid = false,
        FailureReason = reason,
        ErrorMessage = message ?? GetDefaultMessage(reason)
    };

    /// <summary>
    /// Gets the default error message for a failure reason.
    /// </summary>
    /// <param name="reason">The failure reason.</param>
    /// <returns>A default error message.</returns>
    private static string GetDefaultMessage(SsoValidationFailureReason reason) => reason switch
    {
        SsoValidationFailureReason.SessionNotFound => "The session was not found.",
        SsoValidationFailureReason.SessionExpired => "The session has expired.",
        SsoValidationFailureReason.SessionRevoked => "The session has been revoked.",
        SsoValidationFailureReason.TokenInvalid => "The session token is invalid.",
        SsoValidationFailureReason.TokenExpired => "The session token has expired.",
        SsoValidationFailureReason.DeviceMismatch => "The request originated from a different device.",
        SsoValidationFailureReason.IpMismatch => "The request originated from a different IP address.",
        SsoValidationFailureReason.UserDisabled => "The user account has been disabled.",
        SsoValidationFailureReason.UserNotFound => "The user account was not found.",
        SsoValidationFailureReason.DirectoryUnavailable => "The directory service is unavailable.",
        SsoValidationFailureReason.ValidationError => "An error occurred during validation.",
        _ => "Session validation failed."
    };
}

/// <summary>
/// Specifies the reason for session validation failure.
/// </summary>
public enum SsoValidationFailureReason
{
    /// <summary>
    /// The session was not found in the session store.
    /// </summary>
    SessionNotFound = 0,

    /// <summary>
    /// The session has expired.
    /// </summary>
    SessionExpired = 1,

    /// <summary>
    /// The session has been explicitly revoked.
    /// </summary>
    SessionRevoked = 2,

    /// <summary>
    /// The session token is invalid or malformed.
    /// </summary>
    TokenInvalid = 3,

    /// <summary>
    /// The session token has expired.
    /// </summary>
    TokenExpired = 4,

    /// <summary>
    /// The request originated from a different device than the session was created on.
    /// </summary>
    DeviceMismatch = 5,

    /// <summary>
    /// The request originated from a different IP address than the session was created on.
    /// </summary>
    IpMismatch = 6,

    /// <summary>
    /// The user account has been disabled in the directory.
    /// </summary>
    UserDisabled = 7,

    /// <summary>
    /// The user account was not found in the directory.
    /// </summary>
    UserNotFound = 8,

    /// <summary>
    /// The directory service is unavailable.
    /// </summary>
    DirectoryUnavailable = 9,

    /// <summary>
    /// A general validation error occurred.
    /// </summary>
    ValidationError = 10
}
