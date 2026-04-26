namespace AISportCoach.API.DTOs;

/// <summary>
/// Generic paged response DTO for API endpoints.
/// </summary>
/// <typeparam name="TDto">The DTO type for items in the page.</typeparam>
public record PagedResultDto<TDto>(
    List<TDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
