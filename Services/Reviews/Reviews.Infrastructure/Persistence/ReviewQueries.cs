using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

public sealed class ReviewQueries(MongoDbContext context) : IReviewQueries
{
    private static readonly BsonDocument ProjectStage = new("$project", new BsonDocument
    {
        { "_id", 1 },
        { "courseId", 1 },
        { "courseTitle", 1 },
        { "authorId", "$author.userId" },
        { "authorName", "$author.displayName" },
        { "rating", 1 },
        { "text", 1 },
        { "photos", new BsonDocument("$ifNull", new BsonArray { "$photos", new BsonArray() }) },
        {
            "commentsCount",
            new BsonDocument("$size",
                new BsonDocument("$ifNull", new BsonArray { "$comments", new BsonArray() }))
        },
        { "comments", new BsonDocument("$ifNull", new BsonArray { "$comments", new BsonArray() }) },
        { "discussionId", 1 },
        { "version", new BsonDocument("$ifNull", new BsonArray { "$version", 0 }) },
        {
            "createdAt",
            new BsonDocument("$ifNull",
                new BsonArray { "$createdAt", new BsonDateTime(DateTime.UnixEpoch) })
        },
        { "updatedAt", 1 }
    });

    private readonly IMongoCollection<Review> _reviews = context.Reviews;

    public async Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return null;

        var document = await _reviews.Aggregate()
            .AppendStage<BsonDocument>(new BsonDocument("$match", new BsonDocument("_id", objectId)))
            .AppendStage<BsonDocument>(ProjectStage)
            .FirstOrDefaultAsync(ct);

        return document is null ? null : ToDto(document);
    }

    public async Task<PagedResult<ReviewDto>> GetListAsync(
        ReviewFilter filter, CancellationToken ct = default)
    {
        var match = BuildMatch(filter);
        var total = (int)await _reviews.CountDocumentsAsync(match, cancellationToken: ct);

        var sort = new BsonDocument { { "createdAt", -1 }, { "_id", -1 } };
        var skip = Math.Max(filter.Page - 1, 0) * filter.PageSize;

        var documents = await _reviews.Aggregate()
            .AppendStage<BsonDocument>(new BsonDocument("$match", match))
            .AppendStage<BsonDocument>(new BsonDocument("$sort", sort))
            .AppendStage<BsonDocument>(new BsonDocument("$skip", skip))
            .AppendStage<BsonDocument>(new BsonDocument("$limit", filter.PageSize))
            .AppendStage<BsonDocument>(ProjectStage)
            .ToListAsync(ct);

        return new PagedResult<ReviewDto>(
            documents.Select(ToDto).ToList(),
            total,
            filter.Page,
            filter.PageSize);
    }

    private static BsonDocument BuildMatch(ReviewFilter filter)
    {
        var match = new BsonDocument();
        if (filter.CourseId is not null)
            match["courseId"] = filter.CourseId.Value;
        if (!string.IsNullOrWhiteSpace(filter.Search))
            match["$text"] = new BsonDocument("$search", filter.Search.Trim());
        return match;
    }

    private static ReviewDto ToDto(BsonDocument document) => new(
        document["_id"].AsObjectId.ToString(),
        document["courseId"].ToInt32(),
        document.GetValue("courseTitle", "").AsString,
        document.GetValue("authorId", "").AsString,
        document.GetValue("authorName", "").AsString,
        document.GetValue("rating", 0).ToInt32(),
        document.GetValue("text", "").AsString,
        document["photos"].AsBsonArray.Select(photo => photo.AsString).ToList(),
        ToComments(document["comments"].AsBsonArray),
        document.GetValue("commentsCount", 0).ToInt32(),
        document.TryGetValue("discussionId", out var discussionId) && discussionId.IsObjectId
            ? discussionId.AsObjectId.ToString()
            : null,
        document.GetValue("version", 0).ToInt32(),
        document["createdAt"].ToUniversalTime(),
        document.TryGetValue("updatedAt", out var updatedAt) && updatedAt.IsValidDateTime
            ? updatedAt.ToUniversalTime()
            : null);

    private static IReadOnlyList<CommentDto> ToComments(BsonArray comments)
        => comments.Select(comment =>
        {
            var document = comment.AsBsonDocument;
            return new CommentDto(
                document.TryGetValue("_id", out var id) && id.IsObjectId
                    ? id.AsObjectId.ToString()
                    : null,
                document.GetValue("author", "").AsString,
                document.GetValue("text", "").AsString,
                document.TryGetValue("createdAt", out var createdAt) && createdAt.IsValidDateTime
                    ? createdAt.ToUniversalTime()
                    : DateTime.UnixEpoch,
                document.GetValue("version", 0).ToInt32());
        }).ToList();
}
