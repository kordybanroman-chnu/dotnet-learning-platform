using AutoMapper;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Models;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public sealed class UpdateReviewCommandHandler(
    IReviewRepository reviews,
    IMapper mapper) : IRequestHandler<UpdateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(UpdateReviewCommand command, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(command.Id), ct)
            ?? throw new NotFoundException($"Review {command.Id} not found");

        if (command.ExpectedVersion is not null && review.Version != command.ExpectedVersion)
            throw new ConflictException(
                $"Review {review.Id} was modified by another request");

        review.Update(new Rating(command.Rating), command.Text);
        await reviews.UpdateAsync(review, ct);

        return mapper.Map<ReviewDto>(review);
    }
}
