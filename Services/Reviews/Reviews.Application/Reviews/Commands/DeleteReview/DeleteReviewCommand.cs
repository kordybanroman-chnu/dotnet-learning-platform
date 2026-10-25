using Reviews.Application.Common.Messaging;

namespace Reviews.Application.Reviews.Commands.DeleteReview;

public sealed record DeleteReviewCommand(string Id) : ICommand;
