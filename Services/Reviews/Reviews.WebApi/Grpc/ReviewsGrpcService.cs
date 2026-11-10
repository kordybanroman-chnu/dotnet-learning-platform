using FluentValidation;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Grpc;

namespace Reviews.WebApi.Grpc;

public sealed class ReviewsGrpcService(ISender sender) : ReviewsService.ReviewsServiceBase
{
    public override async Task<ReviewsReply> GetForCourses(
        GetForCoursesRequest request, ServerCallContext context)
    {
        if (request.CourseIds.Any(courseId => courseId <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Course ids must be positive"));

        var reply = new ReviewsReply();
        foreach (var courseId in request.CourseIds.Distinct())
        {
            try
            {
                var page = await sender.Send(
                    new GetReviewsListQuery(courseId, null, 1, 100),
                    context.CancellationToken);
                reply.Reviews.AddRange(page.Items.Select(ToReply));
            }
            catch (ValidationException ex)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
            }
        }

        return reply;
    }

    private static ReviewReply ToReply(ReviewDto review) => new()
    {
        Id = review.Id,
        CourseId = review.CourseId,
        CourseTitle = review.CourseTitle,
        AuthorId = review.AuthorId,
        AuthorName = review.AuthorName,
        Rating = review.Rating,
        Text = review.Text,
        Photos = { review.Photos },
        Comments = { review.Comments.Select(ToReply) },
        CommentsCount = review.CommentsCount,
        DiscussionId = review.DiscussionId ?? "",
        Version = review.Version,
        CreatedAt = ToTimestamp(review.CreatedAt),
        UpdatedAt = review.UpdatedAt is null ? null : ToTimestamp(review.UpdatedAt.Value)
    };

    private static ReviewCommentReply ToReply(CommentDto comment) => new()
    {
        Id = comment.Id ?? "",
        Author = comment.Author,
        Text = comment.Text,
        CreatedAt = ToTimestamp(comment.CreatedAt),
        Version = comment.Version
    };

    private static Timestamp ToTimestamp(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => Timestamp.FromDateTime(value),
        DateTimeKind.Local => Timestamp.FromDateTime(value.ToUniversalTime()),
        _ => Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc))
    };
}
