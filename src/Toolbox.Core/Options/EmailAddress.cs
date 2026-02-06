// @file EmailAddress.cs
// @brief Email address representation
// @details Contains an email address with optional display name
// @note Immutable record type for thread safety

namespace Toolbox.Core.Options;

/// <summary>
/// Represents an email address with an optional display name.
/// </summary>
/// <remarks>
/// <para>
/// This is an immutable record type that combines an email address with
/// an optional human-readable display name.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // With display name
/// var sender = new EmailAddress("john.doe@example.com", "John Doe");
///
/// // Without display name
/// var recipient = new EmailAddress("jane@example.com");
/// </code>
/// </example>
/// <param name="Address">The email address (e.g., "user@example.com").</param>
/// <param name="DisplayName">The optional display name (e.g., "John Doe").</param>
public sealed record EmailAddress(string Address, string? DisplayName = null)
{
    /// <summary>
    /// Gets the email address.
    /// </summary>
    /// <value>The email address string.</value>
    public string Address { get; init; } = Address ?? throw new ArgumentNullException(nameof(Address));

    /// <summary>
    /// Implicitly converts a string to an <see cref="EmailAddress"/>.
    /// </summary>
    /// <param name="address">The email address string.</param>
    /// <returns>An <see cref="EmailAddress"/> with no display name.</returns>
    public static implicit operator EmailAddress(string address) => new(address);

    /// <inheritdoc />
    public override string ToString() =>
        string.IsNullOrEmpty(DisplayName)
            ? Address
            : $"\"{DisplayName}\" <{Address}>";
}
