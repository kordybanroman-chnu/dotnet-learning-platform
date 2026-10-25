namespace Catalog.Bll;

public sealed class CourseQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? Category { get; set; }
    public long? InstructorId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string SortBy { get; set; } = "title";
    public string SortDir { get; set; } = "asc";
}
