// @file LdapMoveOptions.cs
// @brief Options for moving LDAP objects between organizational units
// @details Configuration for object relocation and renaming operations

namespace Toolbox.Core.Options;

/// <summary>
/// Options for moving LDAP objects between organizational units.
/// </summary>
/// <remarks>
/// <para>
/// This class provides configuration for moving objects (users, computers, groups)
/// to different organizational units and optionally renaming them.
/// </para>
/// </remarks>
public sealed class LdapMoveOptions
{
    #region Properties

    /// <summary>
    /// Gets or sets the distinguished name of the object to move.
    /// </summary>
    public string? SourceDistinguishedName { get; set; }

    /// <summary>
    /// Gets or sets the distinguished name of the target organizational unit.
    /// </summary>
    /// <remarks>
    /// This should be the DN of the container (OU or CN) where the object
    /// will be moved to.
    /// </remarks>
    public string? TargetOrganizationalUnit { get; set; }

    /// <summary>
    /// Gets or sets the new common name (CN) for the object.
    /// </summary>
    /// <remarks>
    /// If not specified, the original CN is preserved.
    /// </remarks>
    public string? NewCommonName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to delete the old RDN value.
    /// </summary>
    /// <remarks>
    /// This is typically <c>true</c> for most operations.
    /// </remarks>
    public bool DeleteOldRdn { get; set; } = true;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets the target distinguished name for the moved object.
    /// </summary>
    public string? TargetDistinguishedName
    {
        get
        {
            if (string.IsNullOrEmpty(SourceDistinguishedName) || string.IsNullOrEmpty(TargetOrganizationalUnit))
            {
                return null;
            }

            var cn = NewCommonName ?? ExtractCommonName(SourceDistinguishedName);
            return $"CN={EscapeDnComponent(cn)},{TargetOrganizationalUnit}";
        }
    }

    #endregion

    #region Fluent API

    /// <summary>
    /// Creates a new instance of <see cref="LdapMoveOptions"/>.
    /// </summary>
    /// <returns>A new options instance.</returns>
    public static LdapMoveOptions Create() => new();

    /// <summary>
    /// Sets the source object by distinguished name.
    /// </summary>
    /// <param name="dn">The distinguished name of the object to move.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapMoveOptions FromDn(string dn)
    {
        SourceDistinguishedName = dn;
        return this;
    }

    /// <summary>
    /// Sets the target organizational unit.
    /// </summary>
    /// <param name="targetOu">The DN of the target OU.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapMoveOptions ToOrganizationalUnit(string targetOu)
    {
        TargetOrganizationalUnit = targetOu;
        return this;
    }

    /// <summary>
    /// Sets a new common name for the object.
    /// </summary>
    /// <param name="newCn">The new CN value.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapMoveOptions WithNewName(string newCn)
    {
        NewCommonName = newCn;
        return this;
    }

    /// <summary>
    /// Configures whether to keep the old RDN as an attribute.
    /// </summary>
    /// <param name="keep">Whether to keep the old RDN.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapMoveOptions KeepOldRdn(bool keep = true)
    {
        DeleteOldRdn = !keep;
        return this;
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates the options for a move operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the options are invalid.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrEmpty(SourceDistinguishedName))
        {
            throw new InvalidOperationException("SourceDistinguishedName is required.");
        }

        if (string.IsNullOrEmpty(TargetOrganizationalUnit))
        {
            throw new InvalidOperationException("TargetOrganizationalUnit is required.");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Extracts the common name from a distinguished name.
    /// </summary>
    /// <param name="dn">The distinguished name.</param>
    /// <returns>The common name value.</returns>
    private static string ExtractCommonName(string dn)
    {
        if (string.IsNullOrEmpty(dn))
        {
            return string.Empty;
        }

        // Find the first CN= component
        const string cnPrefix = "CN=";
        var startIndex = dn.IndexOf(cnPrefix, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return string.Empty;
        }

        startIndex += cnPrefix.Length;
        var endIndex = dn.IndexOf(',', startIndex);

        var cn = endIndex < 0 ? dn[startIndex..] : dn[startIndex..endIndex];

        // Unescape special characters
        return UnescapeDnComponent(cn);
    }

    /// <summary>
    /// Escapes special characters in a DN component.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value.</returns>
    private static string EscapeDnComponent(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace(",", "\\,")
            .Replace("+", "\\+")
            .Replace("\"", "\\\"")
            .Replace("<", "\\<")
            .Replace(">", "\\>")
            .Replace(";", "\\;")
            .Replace("=", "\\=");
    }

    /// <summary>
    /// Unescapes special characters in a DN component.
    /// </summary>
    /// <param name="value">The value to unescape.</param>
    /// <returns>The unescaped value.</returns>
    private static string UnescapeDnComponent(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace("\\,", ",")
            .Replace("\\+", "+")
            .Replace("\\\"", "\"")
            .Replace("\\<", "<")
            .Replace("\\>", ">")
            .Replace("\\;", ";")
            .Replace("\\=", "=")
            .Replace("\\\\", "\\");
    }

    #endregion
}
