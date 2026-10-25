using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll;

public sealed record CourseDetailDto(string Syllabus, int DurationHours);

public sealed record CourseImageDto(long Id, string Url, int SortOrder);

public sealed record CourseDto(
    long Id, string Slug, string Title, decimal Price, long InstructorId, string InstructorName,
    CourseDetailDto? Detail, IReadOnlyList<CourseImageDto> Images,
    IReadOnlyList<string> Categories, DateTime CreatedAt);

public sealed record CourseListItemDto(
    long Id, string Slug, string Title, decimal Price, string InstructorName,
    IReadOnlyList<string> Categories);

public sealed record CourseShortDto(long Id, string Slug, string Title, decimal Price);

public sealed record CategoryDto(long Id, string Name, string Slug);

public sealed record CategoryWithCoursesDto(
    long Id, string Name, string Slug, IReadOnlyList<CourseShortDto> Courses);

public sealed class CreateCourseDto
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [Required, MaxLength(200)]
    public string Slug { get; set; } = "";

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Price { get; set; }

    [Range(1, long.MaxValue)]
    public long InstructorId { get; set; }

    [MaxLength(4000)]
    public string? Syllabus { get; set; }

    public int? DurationHours { get; set; }

    public List<long> CategoryIds { get; set; } = new();

    [MaxLength(500)]
    public string? ImageUrl { get; set; }
}

public sealed class UpdateCoursePriceDto
{
    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Price { get; set; }
}
