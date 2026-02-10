// @file CredentialStoreProvider.cs
// @brief Enumeration defining available credential storage providers
// @details Used to configure which storage mechanism to use for credentials

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the credential storage provider to use.
/// </summary>
/// <remarks>
/// <para>
/// The SSO system supports multiple credential storage backends:
/// </para>
/// <list type="bullet">
///   <item><description>Windows Credential Manager for Windows platforms</description></item>
///   <item><description>macOS Keychain for Apple platforms</description></item>
///   <item><description>Encrypted file storage as cross-platform fallback</description></item>
///   <item><description>In-memory storage for testing scenarios</description></item>
/// </list>
/// </remarks>
public enum CredentialStoreProvider
{
    /// <summary>
    /// Automatically select the best provider for the current platform.
    /// </summary>
    /// <remarks>
    /// Selection order: Windows Credential Manager → macOS Keychain → Encrypted File.
    /// </remarks>
    Auto = 0,

    /// <summary>
    /// Windows Credential Manager (Windows only).
    /// </summary>
    /// <remarks>
    /// Uses the Windows Credential Manager API via P/Invoke.
    /// Credentials are encrypted using DPAPI.
    /// </remarks>
    WindowsCredentialManager = 1,

    /// <summary>
    /// macOS Keychain (macOS only).
    /// </summary>
    /// <remarks>
    /// Uses the Security.framework keychain API.
    /// </remarks>
    MacOsKeychain = 2,

    /// <summary>
    /// Linux Secret Service (Linux only).
    /// </summary>
    /// <remarks>
    /// Uses the freedesktop.org Secret Service API (GNOME Keyring, KDE Wallet).
    /// </remarks>
    LinuxSecretService = 3,

    /// <summary>
    /// Encrypted file storage (cross-platform).
    /// </summary>
    /// <remarks>
    /// Uses AES-256-GCM encryption with platform-specific key derivation.
    /// </remarks>
    EncryptedFile = 4,

    /// <summary>
    /// In-memory storage (for testing only).
    /// </summary>
    /// <remarks>
    /// Credentials are not persisted and are lost when the application exits.
    /// </remarks>
    InMemory = 5
}
