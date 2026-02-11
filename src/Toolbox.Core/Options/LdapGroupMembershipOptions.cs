// @file LdapGroupMembershipOptions.cs
// @brief Options for LDAP group membership operations
// @details Configuration for adding and removing group members

namespace Toolbox.Core.Options;

/// <summary>
/// Options for LDAP group membership operations.
/// </summary>
/// <remarks>
/// <para>
/// This class provides configuration for managing group membership including:
/// </para>
/// <list type="bullet">
///   <item><description>Adding members to groups</description></item>
///   <item><description>Removing members from groups</description></item>
///   <item><description>Batch operations for multiple members</description></item>
/// </list>
/// </remarks>
public sealed class LdapGroupMembershipOptions
{
    #region Properties

    /// <summary>
    /// Gets or sets the distinguished name of the group.
    /// </summary>
    /// <remarks>
    /// Either this or <see cref="GroupName"/> must be provided.
    /// </remarks>
    public string? GroupDistinguishedName { get; set; }

    /// <summary>
    /// Gets or sets the name of the group.
    /// </summary>
    /// <remarks>
    /// Either this or <see cref="GroupDistinguishedName"/> must be provided.
    /// The service will resolve this to a DN.
    /// </remarks>
    public string? GroupName { get; set; }

    /// <summary>
    /// Gets or sets the distinguished name of the member to add/remove.
    /// </summary>
    /// <remarks>
    /// This can be a user, computer, or nested group DN.
    /// For batch operations, use <see cref="MemberDistinguishedNames"/>.
    /// </remarks>
    public string? MemberDistinguishedName { get; set; }

    /// <summary>
    /// Gets or sets the username of the member to add/remove.
    /// </summary>
    /// <remarks>
    /// If specified instead of <see cref="MemberDistinguishedName"/>,
    /// the service will resolve this to a DN.
    /// </remarks>
    public string? MemberUsername { get; set; }

    /// <summary>
    /// Gets or sets multiple member distinguished names for batch operations.
    /// </summary>
    public IReadOnlyList<string>? MemberDistinguishedNames { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to continue on errors
    /// during batch operations.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, the operation will attempt to process all members
    /// even if some fail. When <c>false</c>, the operation stops at the
    /// first error.
    /// </remarks>
    public bool ContinueOnError { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets a value indicating whether this is a batch operation.
    /// </summary>
    public bool IsBatchOperation => MemberDistinguishedNames is { Count: > 1 };

    /// <summary>
    /// Gets all member DNs to process.
    /// </summary>
    public IEnumerable<string> GetAllMemberDns()
    {
        if (MemberDistinguishedNames != null)
        {
            foreach (var dn in MemberDistinguishedNames)
            {
                yield return dn;
            }
        }
        else if (!string.IsNullOrEmpty(MemberDistinguishedName))
        {
            yield return MemberDistinguishedName;
        }
    }

    #endregion

    #region Fluent API

    /// <summary>
    /// Creates a new instance of <see cref="LdapGroupMembershipOptions"/>.
    /// </summary>
    /// <returns>A new options instance.</returns>
    public static LdapGroupMembershipOptions Create() => new();

    /// <summary>
    /// Sets the target group by distinguished name.
    /// </summary>
    /// <param name="dn">The group distinguished name.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupMembershipOptions ForGroupDn(string dn)
    {
        GroupDistinguishedName = dn;
        return this;
    }

    /// <summary>
    /// Sets the target group by name.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupMembershipOptions ForGroup(string name)
    {
        GroupName = name;
        return this;
    }

    /// <summary>
    /// Sets the member by distinguished name.
    /// </summary>
    /// <param name="dn">The member distinguished name.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupMembershipOptions WithMemberDn(string dn)
    {
        MemberDistinguishedName = dn;
        return this;
    }

    /// <summary>
    /// Sets the member by username.
    /// </summary>
    /// <param name="username">The member username.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupMembershipOptions WithMember(string username)
    {
        MemberUsername = username;
        return this;
    }

    /// <summary>
    /// Sets multiple members for batch operation.
    /// </summary>
    /// <param name="memberDns">The member distinguished names.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupMembershipOptions WithMembers(params string[] memberDns)
    {
        MemberDistinguishedNames = memberDns;
        return this;
    }

    /// <summary>
    /// Sets multiple members for batch operation.
    /// </summary>
    /// <param name="memberDns">The member distinguished names.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupMembershipOptions WithMembers(IEnumerable<string> memberDns)
    {
        MemberDistinguishedNames = memberDns.ToList();
        return this;
    }

    /// <summary>
    /// Configures the operation to continue on errors.
    /// </summary>
    /// <param name="continueOnError">Whether to continue on errors.</param>
    /// <returns>This instance for chaining.</returns>
    public LdapGroupMembershipOptions WithContinueOnError(bool continueOnError = true)
    {
        ContinueOnError = continueOnError;
        return this;
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates the options for a membership operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the options are invalid.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrEmpty(GroupDistinguishedName) && string.IsNullOrEmpty(GroupName))
        {
            throw new InvalidOperationException("Either GroupDistinguishedName or GroupName must be provided.");
        }

        var hasMember = !string.IsNullOrEmpty(MemberDistinguishedName) ||
                        !string.IsNullOrEmpty(MemberUsername) ||
                        MemberDistinguishedNames is { Count: > 0 };

        if (!hasMember)
        {
            throw new InvalidOperationException(
                "At least one member must be specified via MemberDistinguishedName, MemberUsername, or MemberDistinguishedNames.");
        }
    }

    #endregion
}

/// <summary>
/// Result of a batch group membership operation.
/// </summary>
public sealed class LdapGroupMembershipBatchResult
{
    /// <summary>
    /// Gets or sets the total number of members processed.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets or sets the number of successful operations.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Gets or sets the number of failed operations.
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether all operations succeeded.
    /// </summary>
    public bool IsFullSuccess => FailureCount == 0;

    /// <summary>
    /// Gets or sets a value indicating whether any operation succeeded.
    /// </summary>
    public bool IsPartialSuccess => SuccessCount > 0 && FailureCount > 0;

    /// <summary>
    /// Gets or sets the results for each member.
    /// </summary>
    public IReadOnlyList<LdapManagementResult> Results { get; init; } = [];

    /// <summary>
    /// Gets the failed operations.
    /// </summary>
    public IEnumerable<LdapManagementResult> Failures => Results.Where(r => !r.IsSuccess);

    /// <summary>
    /// Gets the successful operations.
    /// </summary>
    public IEnumerable<LdapManagementResult> Successes => Results.Where(r => r.IsSuccess);
}
