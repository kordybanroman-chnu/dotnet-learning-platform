using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed record CreateReviewCommand(
    int CourseId,
    string CourseTitle,
    string AuthorId,
    string AuthorName,
    int Rating,
    string Text,
    string? DiscussionId = null) : ICommand<ReviewDto>;
