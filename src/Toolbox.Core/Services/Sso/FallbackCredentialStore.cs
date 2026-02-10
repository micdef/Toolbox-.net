// @file FallbackCredentialStore.cs
// @brief Cross-platform encrypted file credential store
// @details Stores credentials in an AES-256-GCM encrypted JSON file

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Base;
using Toolbox.Core.Options;
using Toolbox.Core.Telemetry;

namespace Toolbox.Core.Services.Sso;

/// <summary>
/// Cross-platform credential store using AES-256-GCM encrypted file storage.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides a fallback credential store for platforms
/// where native credential managers are not available or preferred.
/// </para>
/// <para>
/// Credentials are stored in a JSON file encrypted with AES-256-GCM.
/// The encryption key is derived from a machine-specific secret using PBKDF2.
/// </para>
/// </remarks>
public sealed class FallbackCredentialStore : BaseAsyncDisposableService, ICredentialStore
{
    #region Fields

    /// <summary>
    /// The activity source for distributed tracing.
    /// </summary>
    private static new readonly ActivitySource ActivitySource = new(
        TelemetryConstants.ActivitySourceName,
        TelemetryConstants.ActivitySourceVersion);

    /// <summary>
    /// The credential store options.
    /// </summary>
    private readonly CredentialStoreOptions _options;

    /// <summary>
    /// The logger instance.
    /// </summary>
    private readonly ILogger<FallbackCredentialStore> _logger;

    /// <summary>
    /// In-memory cache of credentials.
    /// </summary>
    private readonly ConcurrentDictionary<string, SsoCredential> _cache = new();

    /// <summary>
    /// The path to the encrypted credential file.
    /// </summary>
    private readonly string _filePath;

    /// <summary>
    /// The encryption key derived from machine identity.
    /// </summary>
    private readonly byte[] _encryptionKey;

    /// <summary>
    /// Lock for file operations.
    /// </summary>
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// Whether the store has been loaded from disk.
    /// </summary>
    private bool _isLoaded;

    /// <summary>
    /// JSON serialization options.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Key derivation iteration count.
    /// </summary>
    private const int KeyDerivationIterations = 100000;

    /// <summary>
    /// AES key size in bytes.
    /// </summary>
    private const int KeySizeBytes = 32; // 256 bits

    /// <summary>
    /// AES nonce size in bytes.
    /// </summary>
    private const int NonceSizeBytes = 12;

    /// <summary>
    /// AES tag size in bytes.
    /// </summary>
    private const int TagSizeBytes = 16;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="FallbackCredentialStore"/> class.
    /// </summary>
    /// <param name="options">The credential store options.</param>
    /// <param name="logger">The logger instance.</param>
    public FallbackCredentialStore(
        IOptions<CredentialStoreOptions> options,
        ILogger<FallbackCredentialStore> logger)
        : base(nameof(FallbackCredentialStore), logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        // Determine file path
        _filePath = _options.FallbackStorePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                _options.ApplicationName,
                "credentials.enc");

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Derive encryption key
        _encryptionKey = DeriveEncryptionKey();

        _logger.LogInformation(
            "FallbackCredentialStore initialized, file: {FilePath}",
            _filePath);
    }

    #endregion

    #region ICredentialStore - Store Operations

    /// <inheritdoc />
    public async Task StoreCredentialAsync(
        string key,
        SsoCredential credential,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(credential);

        using var activity = ActivitySource.StartActivity("FallbackCredentialStore.StoreCredential");
        activity?.SetTag("credential.key", key);

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        _cache[key] = credential;
        await SaveAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Stored credential for key {Key}", key);
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    /// <inheritdoc />
    public async Task<SsoCredential?> GetCredentialAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var activity = ActivitySource.StartActivity("FallbackCredentialStore.GetCredential");
        activity?.SetTag("credential.key", key);

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (_cache.TryGetValue(key, out var credential))
        {
            _logger.LogDebug("Retrieved credential for key {Key}", key);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return credential;
        }

        _logger.LogDebug("Credential not found for key {Key}", key);
        return null;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveCredentialAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var activity = ActivitySource.StartActivity("FallbackCredentialStore.RemoveCredential");
        activity?.SetTag("credential.key", key);

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (_cache.TryRemove(key, out _))
        {
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Removed credential for key {Key}", key);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<SsoCredential> UpdateCredentialAsync(
        string key,
        Func<SsoCredential?, SsoCredential> updateFunc,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(updateFunc);

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        _cache.TryGetValue(key, out var existing);
        var updated = updateFunc(existing);
        _cache[key] = updated;
        await SaveAsync(cancellationToken).ConfigureAwait(false);

        return updated;
    }

    #endregion

    #region ICredentialStore - Query Operations

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return _cache.ContainsKey(key);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListKeysAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var keys = _cache
            .Where(kvp => string.Equals(kvp.Value.UserId, userId, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        return keys;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListKeysByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        // Simple wildcard matching
        var keys = _cache.Keys
            .Where(k => MatchesPattern(k, pattern))
            .ToList();

        return keys;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SsoCredential>> GetUserCredentialsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var credentials = _cache.Values
            .Where(c => string.Equals(c.UserId, userId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return credentials;
    }

    #endregion

    #region ICredentialStore - Maintenance Operations

    /// <inheritdoc />
    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Cleaned up {Count} expired credentials", expiredKeys.Count);
        }

        return expiredKeys.Count;
    }

    /// <inheritdoc />
    public async Task<int> ClearUserCredentialsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var userKeys = _cache
            .Where(kvp => string.Equals(kvp.Value.UserId, userId, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in userKeys)
        {
            _cache.TryRemove(key, out _);
        }

        if (userKeys.Count > 0)
        {
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Cleared {Count} credentials for user {UserId}", userKeys.Count, userId);
        }

        return userKeys.Count;
    }

    /// <inheritdoc />
    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var count = _cache.Count;
        _cache.Clear();
        await SaveAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogWarning("Cleared all {Count} credentials", count);
    }

    /// <inheritdoc />
    public async Task<int> GetCredentialCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return _cache.Count;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Ensures the credential store is loaded from disk.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded)
        {
            return;
        }

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            await LoadAsync(cancellationToken).ConfigureAwait(false);
            _isLoaded = true;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Loads credentials from the encrypted file.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogDebug("Credential file does not exist, starting fresh");
            return;
        }

        try
        {
            var encryptedData = await File.ReadAllBytesAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var decryptedData = Decrypt(encryptedData);
            var json = Encoding.UTF8.GetString(decryptedData);
            var credentials = JsonSerializer.Deserialize<Dictionary<string, SsoCredential>>(json, JsonOptions);

            if (credentials != null)
            {
                foreach (var kvp in credentials)
                {
                    _cache[kvp.Key] = kvp.Value;
                }
            }

            _logger.LogDebug("Loaded {Count} credentials from file", _cache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load credentials from file, starting fresh");
            _cache.Clear();
        }
    }

    /// <summary>
    /// Saves credentials to the encrypted file.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var credentials = new Dictionary<string, SsoCredential>(_cache);
            var json = JsonSerializer.Serialize(credentials, JsonOptions);
            var plainData = Encoding.UTF8.GetBytes(json);
            var encryptedData = Encrypt(plainData);

            await File.WriteAllBytesAsync(_filePath, encryptedData, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Saved {Count} credentials to file", credentials.Count);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Encrypts data using AES-256-GCM.
    /// </summary>
    /// <param name="plainData">The data to encrypt.</param>
    /// <returns>The encrypted data with prepended nonce and tag.</returns>
    private byte[] Encrypt(byte[] plainData)
    {
        var nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plainData.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(_encryptionKey, TagSizeBytes);
        aes.Encrypt(nonce, plainData, ciphertext, tag);

        // Format: [nonce][tag][ciphertext]
        var result = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSizeBytes + TagSizeBytes, ciphertext.Length);

        return result;
    }

    /// <summary>
    /// Decrypts data using AES-256-GCM.
    /// </summary>
    /// <param name="encryptedData">The encrypted data with prepended nonce and tag.</param>
    /// <returns>The decrypted data.</returns>
    private byte[] Decrypt(byte[] encryptedData)
    {
        if (encryptedData.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new InvalidOperationException("Encrypted data is too short");
        }

        var nonce = new byte[NonceSizeBytes];
        var tag = new byte[TagSizeBytes];
        var ciphertext = new byte[encryptedData.Length - NonceSizeBytes - TagSizeBytes];

        Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSizeBytes);
        Buffer.BlockCopy(encryptedData, NonceSizeBytes, tag, 0, TagSizeBytes);
        Buffer.BlockCopy(encryptedData, NonceSizeBytes + TagSizeBytes, ciphertext, 0, ciphertext.Length);

        var plainData = new byte[ciphertext.Length];

        using var aes = new AesGcm(_encryptionKey, TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plainData);

        return plainData;
    }

    /// <summary>
    /// Derives the encryption key from machine-specific data.
    /// </summary>
    /// <returns>The derived encryption key.</returns>
    private byte[] DeriveEncryptionKey()
    {
        // Get machine-specific data for key derivation
        var machineId = GetMachineIdentifier();
        var salt = Encoding.UTF8.GetBytes($"{_options.ApplicationName}:CredentialStore:Salt");

        // Use PBKDF2 to derive the key
        return Rfc2898DeriveBytes.Pbkdf2(
            machineId,
            salt,
            KeyDerivationIterations,
            HashAlgorithmName.SHA256,
            KeySizeBytes);
    }

    /// <summary>
    /// Gets a machine-specific identifier for key derivation.
    /// </summary>
    /// <returns>A machine-specific identifier.</returns>
    private static byte[] GetMachineIdentifier()
    {
        // Combine multiple sources for a unique machine identifier
        var components = new List<string>
        {
            Environment.MachineName,
            Environment.UserName,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        // On Windows, try to get machine GUID
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid") as string;
                if (!string.IsNullOrEmpty(guid))
                {
                    components.Add(guid);
                }
            }
            catch
            {
                // Ignore registry access errors
            }
        }

        var combined = string.Join("|", components);
        return SHA256.HashData(Encoding.UTF8.GetBytes(combined));
    }

    /// <summary>
    /// Checks if a key matches a wildcard pattern.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <param name="pattern">The pattern with * wildcards.</param>
    /// <returns>True if the key matches the pattern.</returns>
    private static bool MatchesPattern(string key, string pattern)
    {
        if (pattern == "*")
        {
            return true;
        }

        // Simple wildcard matching
        var parts = pattern.Split('*');
        if (parts.Length == 1)
        {
            return string.Equals(key, pattern, StringComparison.OrdinalIgnoreCase);
        }

        var index = 0;
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            var foundIndex = key.IndexOf(part, index, StringComparison.OrdinalIgnoreCase);
            if (foundIndex < 0)
            {
                return false;
            }

            index = foundIndex + part.Length;
        }

        return true;
    }

    #endregion

    #region Disposal

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        // Save any pending changes
        if (_isLoaded && _cache.Count > 0)
        {
            try
            {
                await SaveAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save credentials during disposal");
            }
        }

        _fileLock.Dispose();
        _cache.Clear();

        // Clear encryption key from memory
        Array.Clear(_encryptionKey, 0, _encryptionKey.Length);

        _logger.LogInformation("FallbackCredentialStore disposed");
    }

    #endregion
}
