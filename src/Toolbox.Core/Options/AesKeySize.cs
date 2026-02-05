// @file AesKeySize.cs
// @brief Enumeration of valid AES key sizes
// @details Defines the supported AES key sizes in bits
// @note AES supports 128, 192, and 256-bit keys

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the AES key size in bits.
/// </summary>
/// <remarks>
/// Larger key sizes provide stronger encryption but may have slightly higher performance overhead.
/// AES-256 is recommended for most security-sensitive applications.
/// </remarks>
public enum AesKeySize
{
    /// <summary>
    /// 128-bit key (16 bytes).
    /// </summary>
    /// <remarks>
    /// Provides good security and is the fastest option.
    /// </remarks>
    Aes128 = 128,

    /// <summary>
    /// 192-bit key (24 bytes).
    /// </summary>
    /// <remarks>
    /// Provides stronger security than 128-bit.
    /// </remarks>
    Aes192 = 192,

    /// <summary>
    /// 256-bit key (32 bytes).
    /// </summary>
    /// <remarks>
    /// Provides the strongest security.
    /// Recommended for highly sensitive data.
    /// </remarks>
    Aes256 = 256
}
