namespace AISportCoach.Application.Models;

/// <summary>
/// Generic paged result container for query responses.
/// </summary>
/// <typeparam name="T">The type of items in the page.</typeparam>
public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    /// <summary>
    /// Total number of pages based on TotalCount and PageSize.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Indicates if there are more pages after the current page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Indicates if there are pages before the current page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
