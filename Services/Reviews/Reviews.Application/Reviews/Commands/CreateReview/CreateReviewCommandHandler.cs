using AutoMapper;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandHandler(
    IReviewRepository reviews,
    IMapper mapper) : IRequestHandler<CreateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(CreateReviewCommand command, CancellationToken ct)
    {
        var discussionId = string.IsNullOrWhiteSpace(command.DiscussionId)
            ? (ObjectId?)null
            : ObjectId.Parse(command.DiscussionId);

        var review = new Review(
            command.CourseId,
            command.CourseTitle,
            new Author(command.AuthorId, command.AuthorName),
            new Rating(command.Rating),
            command.Text,
            discussionId);

        await reviews.AddAsync(review, ct);
        return mapper.Map<ReviewDto>(review);
    }
}
