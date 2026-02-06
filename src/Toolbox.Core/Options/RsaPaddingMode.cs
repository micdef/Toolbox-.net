// @file RsaPaddingMode.cs
// @brief Enumeration of RSA padding modes
// @details Defines the supported RSA encryption padding modes
// @note OAEP with SHA-256 is recommended for new applications

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the RSA encryption padding mode.
/// </summary>
/// <remarks>
/// <para>
/// The padding mode affects both security and compatibility.
/// OAEP (Optimal Asymmetric Encryption Padding) is more secure than PKCS#1 v1.5.
/// </para>
/// </remarks>
public enum RsaPaddingMode
{
    /// <summary>
    /// PKCS#1 v1.5 padding.
    /// </summary>
    /// <remarks>
    /// Legacy padding mode. Compatible with older systems but vulnerable to certain attacks.
    /// Not recommended for new applications.
    /// </remarks>
    Pkcs1,

    /// <summary>
    /// OAEP with SHA-1 hash.
    /// </summary>
    /// <remarks>
    /// More secure than PKCS#1 v1.5.
    /// SHA-1 is considered weak; prefer SHA-256 or SHA-384 for new applications.
    /// </remarks>
    OaepSha1,

    /// <summary>
    /// OAEP with SHA-256 hash.
    /// </summary>
    /// <remarks>
    /// Recommended padding mode for most applications.
    /// Provides good security with reasonable performance.
    /// </remarks>
    OaepSha256,

    /// <summary>
    /// OAEP with SHA-384 hash.
    /// </summary>
    /// <remarks>
    /// Stronger than SHA-256 with slightly higher overhead.
    /// </remarks>
    OaepSha384,

    /// <summary>
    /// OAEP with SHA-512 hash.
    /// </summary>
    /// <remarks>
    /// Strongest OAEP variant.
    /// Recommended for highly sensitive data.
    /// </remarks>
    OaepSha512
}
