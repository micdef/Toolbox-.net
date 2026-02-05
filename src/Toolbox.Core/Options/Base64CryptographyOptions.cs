// @file Base64CryptographyOptions.cs
// @brief Configuration options for Base64 cryptography service
// @details Defines options for encoding table selection and padding behavior
// @note Configure via DI or direct instantiation

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for the Base64 cryptography service.
/// </summary>
/// <remarks>
/// These options control the encoding behavior of the Base64 service.
/// </remarks>
public sealed class Base64CryptographyOptions
{
    /// <summary>
    /// The configuration section name for Base64 cryptography options.
    /// </summary>
    public const string SectionName = "Toolbox:Cryptography:Base64";

    /// <summary>
    /// Gets or sets the encoding table to use.
    /// </summary>
    /// <value>
    /// The Base64 encoding table. Defaults to <see cref="Base64EncodingTable.Standard"/>.
    /// </value>
    public Base64EncodingTable EncodingTable { get; set; } = Base64EncodingTable.Standard;

    /// <summary>
    /// Gets or sets a value indicating whether padding should be included.
    /// </summary>
    /// <value>
    /// <c>true</c> to include '=' padding characters; <c>false</c> to omit them.
    /// Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// Padding is required by the Base64 standard but can be omitted in some contexts.
    /// When decoding, the service handles both padded and unpadded input.
    /// </remarks>
    public bool IncludePadding { get; set; } = true;
}
