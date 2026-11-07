namespace Platform.Aggregator.Models;

public sealed record EnrollmentItemDto(long CourseId, string CourseTitle, decimal UnitPrice, int Units);

public sealed record EnrollmentDto(
    long Id, long StudentId, string Status, DateTime EnrolledAt,
    string? Note, string? PreferredSchedule, IReadOnlyList<EnrollmentItemDto> Items);

public sealed record CourseDetailDto(string Syllabus, int DurationHours);

public sealed record CourseImageDto(long Id, string Url, int SortOrder);

public sealed record CourseDto(
    long Id, string Slug, string Title, decimal Price, long InstructorId, string InstructorName,
    CourseDetailDto? Detail, IReadOnlyList<CourseImageDto> Images,
    IReadOnlyList<string> Categories, DateTime CreatedAt);

public sealed record CommentDto(string? Id, string Author, string Text, DateTime CreatedAt, int Version);

public sealed record ReviewDto(
    string Id, int CourseId, string CourseTitle, string AuthorId, string AuthorName,
    int Rating, string Text, IReadOnlyList<string> Photos, IReadOnlyList<CommentDto> Comments,
    int CommentsCount, string? DiscussionId, int Version, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed record FullEnrollmentDto(
    EnrollmentDto Enrollment,
    IReadOnlyList<CourseDto> Courses,
    IReadOnlyList<ReviewDto> Reviews,
    IReadOnlyList<string> Warnings);
