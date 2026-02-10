// @file CredentialStoreOptions.cs
// @brief Configuration options for credential storage
// @details Configures the credential store provider and encryption settings

namespace Toolbox.Core.Options;

/// <summary>
/// Configuration options for the credential store.
/// </summary>
/// <remarks>
/// <para>
/// These options control how credentials are stored securely, including
/// the storage provider and encryption settings.
/// </para>
/// <para>
/// Configuration section: <c>Toolbox:Sso:CredentialStore</c>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddSsoServices(
///     ssoOptions => { },
///     credOptions =>
///     {
///         credOptions.Provider = CredentialStoreProvider.WindowsCredentialManager;
///         credOptions.ApplicationName = "MyApp";
///     });
/// </code>
/// </example>
public sealed class CredentialStoreOptions
{
    /// <summary>
    /// The configuration section name for these options.
    /// </summary>
    public const string SectionName = "Toolbox:Sso:CredentialStore";

    /// <summary>
    /// Gets or sets the credential storage provider to use.
    /// </summary>
    /// <value>Default is <see cref="CredentialStoreProvider.Auto"/>.</value>
    /// <remarks>
    /// When set to <see cref="CredentialStoreProvider.Auto"/>, the system
    /// automatically selects the best provider for the current platform.
    /// </remarks>
    public CredentialStoreProvider Provider { get; set; } = CredentialStoreProvider.Auto;

    /// <summary>
    /// Gets or sets the application name for credential manager entries.
    /// </summary>
    /// <value>Default is "Toolbox".</value>
    /// <remarks>
    /// This name is used as a prefix for credential entries in the
    /// operating system's credential manager.
    /// </remarks>
    public string ApplicationName { get; set; } = "Toolbox";

    /// <summary>
    /// Gets or sets the path for fallback encrypted file storage.
    /// </summary>
    /// <value>
    /// The full path to the credential file, or null to use the default location.
    /// Default location is <c>%APPDATA%/Toolbox/credentials.enc</c> on Windows
    /// or <c>~/.config/toolbox/credentials.enc</c> on Linux/macOS.
    /// </value>
    public string? FallbackStorePath { get; set; }

    /// <summary>
    /// Gets or sets the encryption key for fallback storage.
    /// </summary>
    /// <value>
    /// A base64-encoded encryption key, or null to derive from machine key.
    /// </value>
    /// <remarks>
    /// If not specified, the key is derived using platform-specific mechanisms:
    /// DPAPI on Windows, Keychain-derived on macOS, or machine-specific on Linux.
    /// </remarks>
    public string? EncryptionKey { get; set; }

    /// <summary>
    /// Gets or sets whether to use the OS keychain when available.
    /// </summary>
    /// <value>Default is <c>true</c>.</value>
    /// <remarks>
    /// When enabled on macOS/Linux, the system will attempt to use the
    /// native keychain (macOS Keychain, GNOME Keyring, KDE Wallet).
    /// </remarks>
    public bool UseOsKeychain { get; set; } = true;

    /// <summary>
    /// Gets or sets the duration for caching credentials in memory.
    /// </summary>
    /// <value>Default is 5 minutes.</value>
    /// <remarks>
    /// Credentials are cached in memory to reduce disk/API access.
    /// Set to <see cref="TimeSpan.Zero"/> to disable caching.
    /// </remarks>
    public TimeSpan MemoryCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to encrypt credentials at rest.
    /// </summary>
    /// <value>Default is <c>true</c>.</value>
    /// <remarks>
    /// When disabled, credentials are stored in plain text (not recommended).
    /// </remarks>
    public bool EncryptAtRest { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of credentials to cache in memory.
    /// </summary>
    /// <value>Default is 100.</value>
    public int MaxCachedCredentials { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to validate the credential store on startup.
    /// </summary>
    /// <value>Default is <c>true</c>.</value>
    /// <remarks>
    /// When enabled, the credential store is validated and cleaned up
    /// of expired entries on application startup.
    /// </remarks>
    public bool ValidateOnStartup { get; set; } = true;

    /// <summary>
    /// Validates the options configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configuration is invalid.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApplicationName))
            throw new InvalidOperationException("ApplicationName cannot be empty.");

        if (MemoryCacheDuration < TimeSpan.Zero)
            throw new InvalidOperationException("MemoryCacheDuration cannot be negative.");

        if (MaxCachedCredentials < 0)
            throw new InvalidOperationException("MaxCachedCredentials cannot be negative.");
    }
}
