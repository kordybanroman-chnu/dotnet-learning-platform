namespace Reviews.Application.Common.Models;

public sealed record ReviewDto(
    string Id,
    int CourseId,
    string CourseTitle,
    string AuthorId,
    string AuthorName,
    int Rating,
    string Text,
    IReadOnlyList<string> Photos,
    IReadOnlyList<CommentDto> Comments,
    int CommentsCount,
    string? DiscussionId,
    int Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
