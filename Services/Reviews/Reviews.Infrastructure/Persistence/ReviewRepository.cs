using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Infrastructure.Persistence;

public sealed class ReviewRepository(MongoDbContext context) : IReviewRepository
{
    private readonly IMongoCollection<Review> _reviews = context.Reviews;

    public async Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _reviews.Find(r => r.Id == id).FirstOrDefaultAsync(ct);

    public Task<bool> ExistsAsync(int courseId, string authorId, CancellationToken ct = default)
        => _reviews.Find(r => r.CourseId == courseId && r.Author.UserId == authorId)
            .AnyAsync(ct);

    public Task AddAsync(Review review, CancellationToken ct = default)
        => _reviews.InsertOneAsync(review, cancellationToken: ct);

    public async Task UpdateAsync(Review review, CancellationToken ct = default)
    {
        var expectedVersion = review.Version;
        review.BumpVersion();

        var filter = Builders<Review>.Filter.Eq(r => r.Id, review.Id)
                     & Builders<Review>.Filter.Eq(r => r.Version, expectedVersion);

        var result = await _reviews.ReplaceOneAsync(filter, review, cancellationToken: ct);
        if (result.MatchedCount == 0)
            throw new ConflictException(
                $"Review {review.Id} was modified by another request");
    }

    public async Task DeleteAsync(Review review, CancellationToken ct = default)
    {
        var filter = Builders<Review>.Filter.Eq(r => r.Id, review.Id)
                     & Builders<Review>.Filter.Eq(r => r.Version, review.Version);

        var result = await _reviews.DeleteOneAsync(filter, ct);
        if (result.DeletedCount == 0)
            throw new ConflictException(
                $"Review {review.Id} was modified by another request");
    }
}
