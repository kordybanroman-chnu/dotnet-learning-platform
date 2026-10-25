using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public sealed record UpdateReviewCommand(
    string Id,
    int Rating,
    string Text,
    int? ExpectedVersion = null) : ICommand<ReviewDto>;
