// @file WindowsCredentialStore.cs
// @brief Windows Credential Manager implementation for credential storage
// @details Uses P/Invoke to access Windows Credential Manager APIs with DPAPI encryption

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
/// Credential store implementation using Windows Credential Manager.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses P/Invoke to access Windows Credential Manager APIs.
/// Credentials are stored securely using DPAPI encryption provided by the OS.
/// </para>
/// <para>
/// Credential targets follow the pattern: {ApplicationName}:SSO:{key}
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStore : BaseAsyncDisposableService, ICredentialStore
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
    private readonly ILogger<WindowsCredentialStore> _logger;

    /// <summary>
    /// JSON serialization options.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsCredentialStore"/> class.
    /// </summary>
    /// <param name="options">The credential store options.</param>
    /// <param name="logger">The logger instance.</param>
    public WindowsCredentialStore(
        IOptions<CredentialStoreOptions> options,
        ILogger<WindowsCredentialStore> logger)
        : base(nameof(WindowsCredentialStore), logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        _logger.LogInformation(
            "WindowsCredentialStore initialized for application {App}",
            _options.ApplicationName);
    }

    #endregion

    #region ICredentialStore - Store Operations

    /// <inheritdoc />
    public Task StoreCredentialAsync(
        string key,
        SsoCredential credential,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(credential);

        using var activity = ActivitySource.StartActivity("WindowsCredentialStore.StoreCredential");
        activity?.SetTag("credential.key", key);

        var target = GetTargetName(key);
        var json = JsonSerializer.Serialize(credential, JsonOptions);
        var credentialBytes = Encoding.UTF8.GetBytes(json);

        var cred = new NativeMethods.CREDENTIAL
        {
            Type = NativeMethods.CRED_TYPE_GENERIC,
            TargetName = target,
            UserName = credential.UserId,
            CredentialBlob = Marshal.AllocHGlobal(credentialBytes.Length),
            CredentialBlobSize = (uint)credentialBytes.Length,
            Persist = NativeMethods.CRED_PERSIST_LOCAL_MACHINE,
            Comment = $"Toolbox SSO credential for {credential.UserId}"
        };

        try
        {
            Marshal.Copy(credentialBytes, 0, cred.CredentialBlob, credentialBytes.Length);

            if (!NativeMethods.CredWrite(ref cred, 0))
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"Failed to store credential: {new Win32Exception(error).Message}");
            }

            _logger.LogDebug("Stored credential for key {Key}", key);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        finally
        {
            Marshal.FreeHGlobal(cred.CredentialBlob);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SsoCredential?> GetCredentialAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var activity = ActivitySource.StartActivity("WindowsCredentialStore.GetCredential");
        activity?.SetTag("credential.key", key);

        var target = GetTargetName(key);

        if (!NativeMethods.CredRead(target, NativeMethods.CRED_TYPE_GENERIC, 0, out var credPtr))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == NativeMethods.ERROR_NOT_FOUND)
            {
                _logger.LogDebug("Credential not found for key {Key}", key);
                return Task.FromResult<SsoCredential?>(null);
            }

            throw new InvalidOperationException(
                $"Failed to read credential: {new Win32Exception(error).Message}");
        }

        try
        {
            var cred = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(credPtr);
            if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero)
            {
                return Task.FromResult<SsoCredential?>(null);
            }

            var credentialBytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, credentialBytes, 0, (int)cred.CredentialBlobSize);

            var json = Encoding.UTF8.GetString(credentialBytes);
            var credential = JsonSerializer.Deserialize<SsoCredential>(json, JsonOptions);

            _logger.LogDebug("Retrieved credential for key {Key}", key);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return Task.FromResult(credential);
        }
        finally
        {
            NativeMethods.CredFree(credPtr);
        }
    }

    /// <inheritdoc />
    public Task<bool> RemoveCredentialAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var activity = ActivitySource.StartActivity("WindowsCredentialStore.RemoveCredential");
        activity?.SetTag("credential.key", key);

        var target = GetTargetName(key);

        if (!NativeMethods.CredDelete(target, NativeMethods.CRED_TYPE_GENERIC, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == NativeMethods.ERROR_NOT_FOUND)
            {
                _logger.LogDebug("Credential not found for removal: {Key}", key);
                return Task.FromResult(false);
            }

            throw new InvalidOperationException(
                $"Failed to delete credential: {new Win32Exception(error).Message}");
        }

        _logger.LogDebug("Removed credential for key {Key}", key);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return Task.FromResult(true);
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

        var existing = await GetCredentialAsync(key, cancellationToken).ConfigureAwait(false);
        var updated = updateFunc(existing);
        await StoreCredentialAsync(key, updated, cancellationToken).ConfigureAwait(false);

        return updated;
    }

    #endregion

    #region ICredentialStore - Query Operations

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var credential = await GetCredentialAsync(key, cancellationToken).ConfigureAwait(false);
        return credential != null;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListKeysAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var pattern = $"{_options.ApplicationName}:SSO:*";
        return ListKeysByPatternInternalAsync(pattern, userId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListKeysByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        var fullPattern = $"{_options.ApplicationName}:SSO:{pattern}";
        return ListKeysByPatternInternalAsync(fullPattern, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SsoCredential>> GetUserCredentialsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var keys = await ListKeysAsync(userId, cancellationToken).ConfigureAwait(false);
        var credentials = new List<SsoCredential>();

        foreach (var key in keys)
        {
            var credential = await GetCredentialAsync(key, cancellationToken).ConfigureAwait(false);
            if (credential != null && string.Equals(credential.UserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                credentials.Add(credential);
            }
        }

        return credentials;
    }

    /// <summary>
    /// Lists keys matching a pattern, optionally filtered by user ID.
    /// </summary>
    /// <param name="pattern">The pattern to match.</param>
    /// <param name="userId">Optional user ID filter.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The list of matching keys.</returns>
    private Task<IReadOnlyList<string>> ListKeysByPatternInternalAsync(
        string pattern,
        string? userId,
        CancellationToken cancellationToken)
    {
        var keys = new List<string>();

        if (!NativeMethods.CredEnumerate(pattern.Replace("*", null), 0, out var count, out var credsPtr))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == NativeMethods.ERROR_NOT_FOUND)
            {
                return Task.FromResult<IReadOnlyList<string>>(keys);
            }

            throw new InvalidOperationException(
                $"Failed to enumerate credentials: {new Win32Exception(error).Message}");
        }

        try
        {
            var prefix = $"{_options.ApplicationName}:SSO:";
            for (var i = 0; i < count; i++)
            {
                var credPtr = Marshal.ReadIntPtr(credsPtr, i * IntPtr.Size);
                var cred = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(credPtr);

                if (cred.TargetName != null && cred.TargetName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    // Filter by user if specified
                    if (userId == null || string.Equals(cred.UserName, userId, StringComparison.OrdinalIgnoreCase))
                    {
                        var key = cred.TargetName[prefix.Length..];
                        keys.Add(key);
                    }
                }
            }
        }
        finally
        {
            NativeMethods.CredFree(credsPtr);
        }

        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    #endregion

    #region ICredentialStore - Maintenance Operations

    /// <inheritdoc />
    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var keys = await ListKeysByPatternAsync("*", cancellationToken).ConfigureAwait(false);
        var removed = 0;

        foreach (var key in keys)
        {
            var credential = await GetCredentialAsync(key, cancellationToken).ConfigureAwait(false);
            if (credential?.ExpiresAt != null && credential.ExpiresAt < DateTimeOffset.UtcNow)
            {
                if (await RemoveCredentialAsync(key, cancellationToken).ConfigureAwait(false))
                {
                    removed++;
                }
            }
        }

        _logger.LogInformation("Cleaned up {Count} expired credentials", removed);
        return removed;
    }

    /// <inheritdoc />
    public async Task<int> ClearUserCredentialsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var keys = await ListKeysAsync(userId, cancellationToken).ConfigureAwait(false);
        var removed = 0;

        foreach (var key in keys)
        {
            if (await RemoveCredentialAsync(key, cancellationToken).ConfigureAwait(false))
            {
                removed++;
            }
        }

        _logger.LogInformation("Cleared {Count} credentials for user {UserId}", removed, userId);
        return removed;
    }

    /// <inheritdoc />
    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var keys = await ListKeysByPatternAsync("*", cancellationToken).ConfigureAwait(false);

        foreach (var key in keys)
        {
            await RemoveCredentialAsync(key, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogWarning("Cleared all {Count} credentials", keys.Count);
    }

    /// <inheritdoc />
    public async Task<int> GetCredentialCountAsync(CancellationToken cancellationToken = default)
    {
        var keys = await ListKeysByPatternAsync("*", cancellationToken).ConfigureAwait(false);
        return keys.Count;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Gets the Windows Credential Manager target name for a key.
    /// </summary>
    /// <param name="key">The credential key.</param>
    /// <returns>The target name.</returns>
    private string GetTargetName(string key) => $"{_options.ApplicationName}:SSO:{key}";

    #endregion

    #region Disposal

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WindowsCredentialStore disposed");
        return ValueTask.CompletedTask;
    }

    #endregion

    #region Native Methods

    /// <summary>
    /// Native Windows API methods for credential management.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static class NativeMethods
    {
        /// <summary>
        /// Generic credential type.
        /// </summary>
        public const int CRED_TYPE_GENERIC = 1;

        /// <summary>
        /// Persist credential on local machine.
        /// </summary>
        public const int CRED_PERSIST_LOCAL_MACHINE = 2;

        /// <summary>
        /// Error code for not found.
        /// </summary>
        public const int ERROR_NOT_FOUND = 1168;

        /// <summary>
        /// Credential structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public long LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        /// <summary>
        /// Writes a credential to Windows Credential Manager.
        /// </summary>
        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredWrite(ref CREDENTIAL credential, int flags);

        /// <summary>
        /// Reads a credential from Windows Credential Manager.
        /// </summary>
        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredRead(
            string target,
            int type,
            int reservedFlag,
            out IntPtr credentialPtr);

        /// <summary>
        /// Deletes a credential from Windows Credential Manager.
        /// </summary>
        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredDelete(string target, int type, int flags);

        /// <summary>
        /// Enumerates credentials in Windows Credential Manager.
        /// </summary>
        [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredEnumerate(
            string? filter,
            int flags,
            out int count,
            out IntPtr credentials);

        /// <summary>
        /// Frees a credential structure.
        /// </summary>
        [DllImport("advapi32.dll", EntryPoint = "CredFree")]
        public static extern void CredFree(IntPtr credential);
    }

    #endregion
}
