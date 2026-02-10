// @file SsoSessionState.cs
// @brief Enumeration defining the possible states of an SSO session
// @details Used to track session lifecycle from creation to revocation

namespace Toolbox.Core.Options;

/// <summary>
/// Specifies the state of an SSO session.
/// </summary>
/// <remarks>
/// <para>
/// Sessions transition through states during their lifecycle:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Active"/> - Session is valid and can be used</description></item>
///   <item><description><see cref="Refreshing"/> - Token refresh is in progress</description></item>
///   <item><description><see cref="Expiring"/> - Session will expire soon</description></item>
///   <item><description><see cref="Expired"/> - Session has expired naturally</description></item>
///   <item><description><see cref="Revoked"/> - Session was explicitly revoked</description></item>
/// </list>
/// </remarks>
public enum SsoSessionState
{
    /// <summary>
    /// Session is active and valid for use.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Session token refresh is in progress.
    /// </summary>
    Refreshing = 1,

    /// <summary>
    /// Session is about to expire (within warning threshold).
    /// </summary>
    Expiring = 2,

    /// <summary>
    /// Session has expired naturally.
    /// </summary>
    Expired = 3,

    /// <summary>
    /// Session was explicitly revoked by user or system.
    /// </summary>
    Revoked = 4
}
