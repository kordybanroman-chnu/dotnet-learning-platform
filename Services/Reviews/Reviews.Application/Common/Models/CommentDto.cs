namespace Reviews.Application.Common.Models;

public sealed record CommentDto(
    string? Id,
    string Author,
    string Text,
    DateTime CreatedAt,
    int Version);
