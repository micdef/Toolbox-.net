// @file RsaKeySize.cs
// @brief Enumeration of valid RSA key sizes
// @details Defines the supported RSA key sizes in bits
// @note RSA supports 512, 1024, 2048, 4096, 8192, and 16384-bit keys

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the RSA key size in bits.
/// </summary>
/// <remarks>
/// <para>
/// Larger key sizes provide stronger security but have significantly higher performance overhead.
/// </para>
/// <para>
/// RSA-2048 is the minimum recommended for production use.
/// RSA-4096 is recommended for highly sensitive data.
/// </para>
/// </remarks>
public enum RsaKeySize
{
    /// <summary>
    /// 512-bit key.
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> This key size is considered insecure and should only be used for testing.
    /// </remarks>
    Rsa512 = 512,

    /// <summary>
    /// 1024-bit key.
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> This key size is considered weak and not recommended for production use.
    /// </remarks>
    Rsa1024 = 1024,

    /// <summary>
    /// 2048-bit key.
    /// </summary>
    /// <remarks>
    /// Minimum recommended key size for production use.
    /// Provides good balance between security and performance.
    /// </remarks>
    Rsa2048 = 2048,

    /// <summary>
    /// 4096-bit key.
    /// </summary>
    /// <remarks>
    /// Recommended for highly sensitive data.
    /// Provides stronger security with moderate performance impact.
    /// </remarks>
    Rsa4096 = 4096,

    /// <summary>
    /// 8192-bit key.
    /// </summary>
    /// <remarks>
    /// Very strong security with significant performance impact.
    /// Use only when maximum security is required.
    /// </remarks>
    Rsa8192 = 8192,

    /// <summary>
    /// 16384-bit key.
    /// </summary>
    /// <remarks>
    /// Maximum security with very high performance impact.
    /// Key generation and operations can be very slow.
    /// </remarks>
    Rsa16384 = 16384
}
