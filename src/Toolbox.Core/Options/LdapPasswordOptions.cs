// @file LdapPasswordOptions.cs
// @brief Options for LDAP password operations
// @details Configuration for password change, reset, and policy settings

namespace Toolbox.Core.Options;

/// <summary>
/// Options for LDAP password operations.
/// </summary>
/// <remarks>
/// <para>
/// This class provides configuration for various password operations including:
/// </para>
/// <list type="bullet">
///   <item><description>User-initiated password change (requires old password)</description></item>
///   <item><description>Administrative password reset</description></item>
///   <item><description>Force password change at next logon</description></item>
///   <item><description>Password policy configuration</description></item>
/// </list>
/// </remarks>
public sealed class LdapPasswordOptions
{
    #region Properties

    /// <summary>
    /// Gets or sets the distinguished name of the user.
    /// </summary>
    /// <remarks>
    /// Either this or <see cref="Username"/> must be provided.
    /// </remarks>
    public string? DistinguishedName { get; set; }

    /// <summary>
    /// Gets or sets the username (sAMAccountName or uid).
    /// </summary>
    /// <remarks>
    /// Either this or <see cref="DistinguishedName"/> must be provided.
    /// </remarks>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the current password (required for user-initiated change).
    /// </summary>
    /// <remarks>
    /// Only required when <see cref="IsAdministrativeReset"/> is <c>false</c>.
    /// </remarks>
    public string? CurrentPassword { get; set; }

    /// <summary>
    /// Gets or sets the new password.
    /// </summary>
    public string? NewPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is an administrative reset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>true</c>, the operation does not require the current password.
    /// The service account must have sufficient privileges to reset passwords.
    /// </para>
    /// </remarks>
    public bool IsAdministrativeReset { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user must change
    /// password at next logon.
    /// </summary>
    public bool MustChangePasswordAtNextLogon { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to unlock the account
    /// as part of the password operation.
    /// </summary>
    public bool UnlockAccount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the password should never expire.
    /// </summary>
    public bool? PasswordNeverExpires { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user cannot change the password.
    /// </summary>
    public bool? UserCannotChangePassword { get; set; }

    #endregion

    #region Fluent API

    /// <summary>
    /// Creates a new instance of <see cref="LdapPasswordOptions"/>.
    /// </summary>
    /// <returns>A new options instance.</returns>
    public static LdapPasswordOptions Create() => new();

    /// <summary>
    /// Sets the target user by distinguished name.
    /// </summary>
    /// <param name="dn">The distinguished name.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapPasswordOptions ForUserDn(string dn)
    {
        DistinguishedName = dn;
        return this;
    }

    /// <summary>
    /// Sets the target user by username.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapPasswordOptions ForUsername(string username)
    {
        Username = username;
        return this;
    }

    /// <summary>
    /// Sets the password change parameters (user-initiated).
    /// </summary>
    /// <param name="currentPassword">The current password.</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapPasswordOptions WithPasswordChange(string currentPassword, string newPassword)
    {
        CurrentPassword = currentPassword;
        NewPassword = newPassword;
        IsAdministrativeReset = false;
        return this;
    }

    /// <summary>
    /// Sets the password for administrative reset.
    /// </summary>
    /// <param name="newPassword">The new password.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapPasswordOptions WithAdministrativeReset(string newPassword)
    {
        NewPassword = newPassword;
        IsAdministrativeReset = true;
        return this;
    }

    /// <summary>
    /// Requires the user to change password at next logon.
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapPasswordOptions RequireChangeAtNextLogon()
    {
        MustChangePasswordAtNextLogon = true;
        return this;
    }

    /// <summary>
    /// Unlocks the account as part of the operation.
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapPasswordOptions WithUnlock()
    {
        UnlockAccount = true;
        return this;
    }

    /// <summary>
    /// Sets the password to never expire.
    /// </summary>
    /// <param name="neverExpires">Whether the password should never expire.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapPasswordOptions WithPasswordNeverExpires(bool neverExpires = true)
    {
        PasswordNeverExpires = neverExpires;
        return this;
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates the options for a password change operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the options are invalid.
    /// </exception>
    public void ValidateForChange()
    {
        if (string.IsNullOrEmpty(DistinguishedName) && string.IsNullOrEmpty(Username))
        {
            throw new InvalidOperationException("Either DistinguishedName or Username must be provided.");
        }

        if (string.IsNullOrEmpty(NewPassword))
        {
            throw new InvalidOperationException("NewPassword is required.");
        }

        if (!IsAdministrativeReset && string.IsNullOrEmpty(CurrentPassword))
        {
            throw new InvalidOperationException("CurrentPassword is required for non-administrative password change.");
        }
    }

    #endregion
}
