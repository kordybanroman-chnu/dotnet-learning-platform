using FluentValidation;
using MongoDB.Bson;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator(IReviewRepository reviews)
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.CourseTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AuthorId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.DiscussionId)
            .Must(id => ObjectId.TryParse(id, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.DiscussionId))
            .WithMessage("DiscussionId must be a valid ObjectId");

        RuleFor(x => x)
            .MustAsync(async (command, ct) =>
                !await reviews.ExistsAsync(command.CourseId, command.AuthorId, ct))
            .WithMessage("You have already reviewed this course");
    }
}
