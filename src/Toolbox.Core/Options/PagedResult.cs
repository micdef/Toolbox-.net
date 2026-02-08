// @file PagedResult.cs
// @brief Generic paged result container
// @details Provides pagination support for LDAP queries
// @note Used by all LDAP services for paginated responses

namespace Toolbox.Core.Options;

/// <summary>
/// Represents a paginated result set.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
/// <remarks>
/// <para>
/// This class provides pagination metadata along with the actual results,
/// enabling efficient navigation through large datasets.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var result = await ldapService.GetAllUsersAsync(page: 1, pageSize: 50);
/// Console.WriteLine($"Page {result.Page} of {result.TotalPages}");
/// Console.WriteLine($"Showing {result.Items.Count} of {result.TotalCount} users");
///
/// foreach (var user in result.Items)
/// {
///     Console.WriteLine(user.DisplayName);
/// }
/// </code>
/// </example>
public sealed class PagedResult<T>
{
    /// <summary>
    /// Gets or sets the items in the current page.
    /// </summary>
    public IReadOnlyList<T> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the total number of items across all pages.
    /// </summary>
    /// <remarks>
    /// May be -1 if the total count is unknown or expensive to compute.
    /// </remarks>
    public int TotalCount { get; set; } = -1;

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    /// <value>
    /// The total pages, or -1 if <see cref="TotalCount"/> is unknown.
    /// </value>
    public int TotalPages => TotalCount < 0
        ? -1
        : (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage => TotalCount < 0
        ? Items.Count >= PageSize
        : Page < TotalPages;

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Gets or sets a continuation token for cursor-based pagination.
    /// </summary>
    /// <remarks>
    /// Used by services that support cursor-based pagination (e.g., Azure AD).
    /// Pass this token to the next request to get the next page.
    /// </remarks>
    public string? ContinuationToken { get; set; }

    /// <summary>
    /// Creates an empty paged result.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>An empty paged result.</returns>
    public static PagedResult<T> Empty(int page = 1, int pageSize = 50) => new()
    {
        Items = [],
        Page = page,
        PageSize = pageSize,
        TotalCount = 0
    };

    /// <summary>
    /// Creates a paged result from a collection.
    /// </summary>
    /// <param name="items">The items for the current page.</param>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="totalCount">The total count (or -1 if unknown).</param>
    /// <returns>A paged result.</returns>
    public static PagedResult<T> Create(
        IEnumerable<T> items,
        int page,
        int pageSize,
        int totalCount = -1) => new()
    {
        Items = items.ToList().AsReadOnly(),
        Page = page,
        PageSize = pageSize,
        TotalCount = totalCount
    };
}
