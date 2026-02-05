// @file ICryptographyService.cs
// @brief Interface for cryptography services
// @details Defines the contract for text encryption and decryption operations
// @note All implementations must support both synchronous and asynchronous operations

namespace Toolbox.Core.Abstractions.Services;

/// <summary>
/// Interface for cryptography services providing text encryption and decryption.
/// </summary>
/// <remarks>
/// <para>
/// This interface defines a simple contract for encrypting and decrypting text.
/// Implementations may use various algorithms (Base64, AES, RSA, etc.).
/// </para>
/// <para>
/// All methods accept and return string values for ease of use.
/// </para>
/// </remarks>
public interface ICryptographyService : IInstrumentedService, IAsyncDisposableService
{
    /// <summary>
    /// Encrypts the specified plain text.
    /// </summary>
    /// <param name="plainText">The text to encrypt.</param>
    /// <returns>The encrypted text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="plainText"/> is <c>null</c>.</exception>
    string Encrypt(string plainText);

    /// <summary>
    /// Asynchronously encrypts the specified plain text.
    /// </summary>
    /// <param name="plainText">The text to encrypt.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the encrypted text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="plainText"/> is <c>null</c>.</exception>
    Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts the specified encrypted text.
    /// </summary>
    /// <param name="encryptedText">The text to decrypt.</param>
    /// <returns>The decrypted plain text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="encryptedText"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">Thrown when the encrypted text format is invalid.</exception>
    string Decrypt(string encryptedText);

    /// <summary>
    /// Asynchronously decrypts the specified encrypted text.
    /// </summary>
    /// <param name="encryptedText">The text to decrypt.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the decrypted plain text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="encryptedText"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">Thrown when the encrypted text format is invalid.</exception>
    Task<string> DecryptAsync(string encryptedText, CancellationToken cancellationToken = default);
}
