using MediatR;
using MongoDB.Bson;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.DeleteReview;

public sealed class DeleteReviewCommandHandler(IReviewRepository reviews)
    : IRequestHandler<DeleteReviewCommand>
{
    public async Task Handle(DeleteReviewCommand command, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(command.Id), ct)
            ?? throw new NotFoundException($"Review {command.Id} not found");

        await reviews.DeleteAsync(review, ct);
    }
}
