using System.Diagnostics;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandHandler(
    IReviewRepository reviews,
    IMapper mapper,
    ILogger<CreateReviewCommandHandler> logger) : IRequestHandler<CreateReviewCommand, ReviewDto>
{
    private static readonly ActivitySource ActivitySource = new("Reviews.WebApi");

    public async Task<ReviewDto> Handle(CreateReviewCommand command, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("CreateReview");
        activity?.SetTag("course.id", command.CourseId);
        activity?.SetTag("review.rating", command.Rating);

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

        activity?.SetTag("review.id", review.Id.ToString());
        logger.LogInformation("Created review {ReviewId} for course {CourseId} with rating {Rating}",
            review.Id, command.CourseId, command.Rating);

        return mapper.Map<ReviewDto>(review);
    }
}
