namespace Reviews.Application.Common.Models;

public sealed record ReviewFilter(
    int? CourseId,
    string? Search,
    int Page,
    int PageSize);
