// @file LdapAccountOptions.cs
// @brief Options for LDAP account management operations
// @details Configuration for enabling, disabling, and managing account status

namespace Toolbox.Core.Options;

/// <summary>
/// Options for LDAP account management operations.
/// </summary>
/// <remarks>
/// <para>
/// This class provides configuration for account management operations including:
/// </para>
/// <list type="bullet">
///   <item><description>Enabling and disabling accounts</description></item>
///   <item><description>Unlocking accounts</description></item>
///   <item><description>Setting account expiration</description></item>
///   <item><description>Modifying account flags</description></item>
/// </list>
/// </remarks>
public sealed class LdapAccountOptions
{
    #region Properties

    /// <summary>
    /// Gets or sets the distinguished name of the account.
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
    /// Gets or sets the type of object (user or computer).
    /// </summary>
    public LdapObjectType ObjectType { get; set; } = LdapObjectType.User;

    /// <summary>
    /// Gets or sets the account expiration date.
    /// </summary>
    /// <remarks>
    /// Set to <c>null</c> to clear expiration (account never expires).
    /// </remarks>
    public DateTimeOffset? ExpirationDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to clear the account expiration.
    /// </summary>
    public bool ClearExpiration { get; set; }

    #endregion

    #region Fluent API

    /// <summary>
    /// Creates a new instance of <see cref="LdapAccountOptions"/>.
    /// </summary>
    /// <returns>A new options instance.</returns>
    public static LdapAccountOptions Create() => new();

    /// <summary>
    /// Creates options for a user account.
    /// </summary>
    /// <param name="usernameOrDn">The username or distinguished name.</param>
    /// <returns>A new options instance.</returns>
    public static LdapAccountOptions ForUser(string usernameOrDn)
    {
        var options = new LdapAccountOptions { ObjectType = LdapObjectType.User };

        if (usernameOrDn.Contains('='))
        {
            options.DistinguishedName = usernameOrDn;
        }
        else
        {
            options.Username = usernameOrDn;
        }

        return options;
    }

    /// <summary>
    /// Creates options for a computer account.
    /// </summary>
    /// <param name="computerNameOrDn">The computer name or distinguished name.</param>
    /// <returns>A new options instance.</returns>
    public static LdapAccountOptions ForComputer(string computerNameOrDn)
    {
        var options = new LdapAccountOptions { ObjectType = LdapObjectType.Computer };

        if (computerNameOrDn.Contains('='))
        {
            options.DistinguishedName = computerNameOrDn;
        }
        else
        {
            options.Username = computerNameOrDn;
        }

        return options;
    }

    /// <summary>
    /// Sets the target by distinguished name.
    /// </summary>
    /// <param name="dn">The distinguished name.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapAccountOptions WithDn(string dn)
    {
        DistinguishedName = dn;
        return this;
    }

    /// <summary>
    /// Sets the target by username.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapAccountOptions WithUsername(string username)
    {
        Username = username;
        return this;
    }

    /// <summary>
    /// Sets the account expiration date.
    /// </summary>
    /// <param name="expirationDate">The expiration date.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapAccountOptions WithExpiration(DateTimeOffset expirationDate)
    {
        ExpirationDate = expirationDate;
        ClearExpiration = false;
        return this;
    }

    /// <summary>
    /// Sets the account to never expire.
    /// </summary>
    /// <returns>This instance for chaining.</returns>
    public LdapAccountOptions WithNoExpiration()
    {
        ExpirationDate = null;
        ClearExpiration = true;
        return this;
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates the options for an account operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the options are invalid.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrEmpty(DistinguishedName) && string.IsNullOrEmpty(Username))
        {
            throw new InvalidOperationException("Either DistinguishedName or Username must be provided.");
        }
    }

    #endregion
}

/// <summary>
/// Defines the types of LDAP objects.
/// </summary>
public enum LdapObjectType
{
    /// <summary>
    /// User account.
    /// </summary>
    User,

    /// <summary>
    /// Computer account.
    /// </summary>
    Computer,

    /// <summary>
    /// Group.
    /// </summary>
    Group,

    /// <summary>
    /// Organizational unit.
    /// </summary>
    OrganizationalUnit,

    /// <summary>
    /// Contact object.
    /// </summary>
    Contact,

    /// <summary>
    /// Other or unknown object type.
    /// </summary>
    Other
}
