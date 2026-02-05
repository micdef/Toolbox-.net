// @file Base64EncodingTable.cs
// @brief Enumeration of Base64 encoding tables
// @details Defines the available Base64 alphabet variants
// @note Standard and UrlSafe are the most commonly used tables

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the Base64 encoding table to use.
/// </summary>
/// <remarks>
/// Different encoding tables are suitable for different use cases.
/// The URL-safe variant is recommended for use in URLs and filenames.
/// </remarks>
public enum Base64EncodingTable
{
    /// <summary>
    /// Standard Base64 alphabet as defined in RFC 4648.
    /// Uses '+' and '/' characters, with '=' padding.
    /// </summary>
    /// <remarks>
    /// Alphabet: A-Z, a-z, 0-9, +, /
    /// </remarks>
    Standard = 0,

    /// <summary>
    /// URL and filename safe Base64 alphabet as defined in RFC 4648 Section 5.
    /// Uses '-' and '_' characters instead of '+' and '/'.
    /// </summary>
    /// <remarks>
    /// Alphabet: A-Z, a-z, 0-9, -, _
    /// This variant is safe for use in URLs and filenames.
    /// </remarks>
    UrlSafe = 1
}
