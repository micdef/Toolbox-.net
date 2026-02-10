// @file ICredentialStore.cs
// @brief Interface for secure credential storage
// @details Defines the contract for storing, retrieving, and managing credentials securely

using Toolbox.Core.Options;

namespace Toolbox.Core.Abstractions.Services;

/// <summary>
/// Defines the contract for secure credential storage.
/// </summary>
/// <remarks>
/// <para>
/// The credential store provides a platform-agnostic interface for storing
/// sensitive authentication credentials securely. Implementations use
/// platform-specific mechanisms:
/// </para>
/// <list type="bullet">
///   <item><description>Windows: Credential Manager with DPAPI encryption</description></item>
///   <item><description>macOS: Keychain Services</description></item>
///   <item><description>Linux: Secret Service API (GNOME Keyring, KDE Wallet)</description></item>
///   <item><description>Fallback: AES-256-GCM encrypted file</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// public class TokenService
/// {
///     private readonly ICredentialStore _store;
///
///     public async Task SaveTokenAsync(string userId, string token, DateTimeOffset expiresAt)
///     {
///         var credential = new SsoCredential
///         {
///             UserId = userId,
///             Type = CredentialType.AccessToken,
///             AccessToken = token,
///             ExpiresAt = expiresAt
///         };
///         await _store.StoreCredentialAsync($"token:{userId}", credential);
///     }
///
///     public async Task&lt;string?&gt; GetTokenAsync(string userId)
///     {
///         var credential = await _store.GetCredentialAsync($"token:{userId}");
///         return credential?.AccessToken;
///     }
/// }
/// </code>
/// </example>
public interface ICredentialStore : IAsyncDisposableService
{
    #region Store Operations

    /// <summary>
    /// Stores a credential securely.
    /// </summary>
    /// <param name="key">The unique key for the credential.</param>
    /// <param name="credential">The credential to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> or <paramref name="credential"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the credential cannot be stored.
    /// </exception>
    Task StoreCredentialAsync(
        string key,
        SsoCredential credential,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a stored credential.
    /// </summary>
    /// <param name="key">The unique key for the credential.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The credential if found, or null.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> is null or empty.
    /// </exception>
    Task<SsoCredential?> GetCredentialAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a stored credential.
    /// </summary>
    /// <param name="key">The unique key for the credential.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><c>true</c> if the credential was removed; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> is null or empty.
    /// </exception>
    Task<bool> RemoveCredentialAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing credential or creates it if it doesn't exist.
    /// </summary>
    /// <param name="key">The unique key for the credential.</param>
    /// <param name="updateFunc">A function to update the credential.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The updated credential.</returns>
    Task<SsoCredential> UpdateCredentialAsync(
        string key,
        Func<SsoCredential?, SsoCredential> updateFunc,
        CancellationToken cancellationToken = default);

    #endregion

    #region Query Operations

    /// <summary>
    /// Checks if a credential exists.
    /// </summary>
    /// <param name="key">The unique key for the credential.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><c>true</c> if the credential exists; otherwise, <c>false</c>.</returns>
    Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all credential keys for a user.
    /// </summary>
    /// <param name="userId">The user ID to list keys for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of credential keys.</returns>
    Task<IReadOnlyList<string>> ListKeysAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all credential keys matching a pattern.
    /// </summary>
    /// <param name="pattern">The pattern to match (supports * wildcard).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of matching credential keys.</returns>
    Task<IReadOnlyList<string>> ListKeysByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all credentials for a user.
    /// </summary>
    /// <param name="userId">The user ID to retrieve credentials for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of credentials.</returns>
    Task<IReadOnlyList<SsoCredential>> GetUserCredentialsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Maintenance Operations

    /// <summary>
    /// Removes all expired credentials.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of credentials removed.</returns>
    Task<int> CleanupExpiredAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all credentials for a user.
    /// </summary>
    /// <param name="userId">The user ID to remove credentials for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of credentials removed.</returns>
    Task<int> ClearUserCredentialsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all stored credentials.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    /// <remarks>
    /// This operation is destructive and cannot be undone.
    /// Use with caution.
    /// </remarks>
    Task ClearAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of stored credentials.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of stored credentials.</returns>
    Task<int> GetCredentialCountAsync(
        CancellationToken cancellationToken = default);

    #endregion
}
