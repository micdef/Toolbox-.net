// @file AzureAdAuthMode.cs
// @brief Enumeration of Azure AD authentication modes
// @details Defines the available authentication methods for Azure AD
// @note Used by the AzureAdService for Microsoft Graph API authentication

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the authentication mode for Azure Active Directory.
/// </summary>
/// <remarks>
/// <para>
/// This enumeration defines how the application authenticates
/// with Azure AD to access Microsoft Graph API:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="ClientSecret"/>: Application secret (simplest)</description></item>
///   <item><description><see cref="Certificate"/>: X.509 certificate (more secure)</description></item>
///   <item><description><see cref="ManagedIdentity"/>: Azure managed identity (for Azure-hosted apps)</description></item>
/// </list>
/// </remarks>
public enum AzureAdAuthMode
{
    /// <summary>
    /// Client secret authentication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses a client secret (application password) for authentication.
    /// This is the simplest method but requires secure storage of the secret.
    /// </para>
    /// </remarks>
    ClientSecret = 0,

    /// <summary>
    /// Certificate-based authentication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses an X.509 certificate for authentication.
    /// More secure than client secret as the private key never leaves the system.
    /// </para>
    /// </remarks>
    Certificate = 1,

    /// <summary>
    /// Managed identity authentication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses Azure Managed Identity for authentication.
    /// Only available for applications hosted in Azure (App Service, Functions, VMs, etc.).
    /// No credentials need to be stored in the application.
    /// </para>
    /// </remarks>
    ManagedIdentity = 2
}
