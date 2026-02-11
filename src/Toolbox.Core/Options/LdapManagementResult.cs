// @file LdapManagementResult.cs
// @brief Result class for LDAP management operations
// @details Contains success status, error information, and operation details

namespace Toolbox.Core.Options;

/// <summary>
/// Represents the result of an LDAP management operation.
/// </summary>
/// <remarks>
/// <para>
/// This class encapsulates the outcome of management operations such as
/// enabling/disabling accounts, modifying group membership, moving objects,
/// and password operations.
/// </para>
/// </remarks>
public sealed class LdapManagementResult
{
    #region Properties

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the type of operation that was performed.
    /// </summary>
    public LdapManagementOperation Operation { get; init; }

    /// <summary>
    /// Gets the distinguished name of the target object.
    /// </summary>
    public string? TargetDistinguishedName { get; init; }

    /// <summary>
    /// Gets the error code if the operation failed.
    /// </summary>
    /// <remarks>
    /// This typically corresponds to the LDAP result code.
    /// </remarks>
    public int? ErrorCode { get; init; }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets additional details about the operation.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Gets the timestamp when the operation was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="operation">The operation type.</param>
    /// <param name="targetDn">The target distinguished name.</param>
    /// <param name="details">Optional operation details.</param>
    /// <returns>A successful result instance.</returns>
    public static LdapManagementResult Success(
        LdapManagementOperation operation,
        string? targetDn = null,
        string? details = null)
    {
        return new LdapManagementResult
        {
            IsSuccess = true,
            Operation = operation,
            TargetDistinguishedName = targetDn,
            Details = details
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="operation">The operation type.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="errorCode">Optional LDAP error code.</param>
    /// <param name="targetDn">The target distinguished name.</param>
    /// <returns>A failed result instance.</returns>
    public static LdapManagementResult Failure(
        LdapManagementOperation operation,
        string errorMessage,
        int? errorCode = null,
        string? targetDn = null)
    {
        return new LdapManagementResult
        {
            IsSuccess = false,
            Operation = operation,
            TargetDistinguishedName = targetDn,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Creates a not supported result.
    /// </summary>
    /// <param name="operation">The operation type.</param>
    /// <param name="directoryType">The directory type that doesn't support this operation.</param>
    /// <param name="details">Optional additional details about why the operation is not supported.</param>
    /// <returns>A not supported result instance.</returns>
    public static LdapManagementResult NotSupported(
        LdapManagementOperation operation,
        LdapDirectoryType directoryType,
        string? details = null)
    {
        return new LdapManagementResult
        {
            IsSuccess = false,
            Operation = operation,
            ErrorMessage = $"Operation '{operation}' is not supported by {directoryType}.",
            Details = details
        };
    }

    #endregion
}

/// <summary>
/// Defines the types of LDAP management operations.
/// </summary>
public enum LdapManagementOperation
{
    /// <summary>
    /// Enable a user or computer account.
    /// </summary>
    EnableAccount,

    /// <summary>
    /// Disable a user or computer account.
    /// </summary>
    DisableAccount,

    /// <summary>
    /// Unlock a locked user account.
    /// </summary>
    UnlockAccount,

    /// <summary>
    /// Add a member to a group.
    /// </summary>
    AddToGroup,

    /// <summary>
    /// Remove a member from a group.
    /// </summary>
    RemoveFromGroup,

    /// <summary>
    /// Move an object to a different organizational unit.
    /// </summary>
    MoveObject,

    /// <summary>
    /// Rename an object (change its common name).
    /// </summary>
    RenameObject,

    /// <summary>
    /// Change a user's password.
    /// </summary>
    ChangePassword,

    /// <summary>
    /// Reset a user's password (administrative reset).
    /// </summary>
    ResetPassword,

    /// <summary>
    /// Force the user to change password at next logon.
    /// </summary>
    ForcePasswordChange,

    /// <summary>
    /// Set password to never expire.
    /// </summary>
    SetPasswordNeverExpires,

    /// <summary>
    /// Clear the password never expires flag.
    /// </summary>
    ClearPasswordNeverExpires,

    /// <summary>
    /// Set account expiration date.
    /// </summary>
    SetAccountExpiration,

    /// <summary>
    /// Clear account expiration (account never expires).
    /// </summary>
    ClearAccountExpiration,

    /// <summary>
    /// Modify an attribute value.
    /// </summary>
    ModifyAttribute,

    /// <summary>
    /// Delete an object from the directory.
    /// </summary>
    DeleteObject
}
