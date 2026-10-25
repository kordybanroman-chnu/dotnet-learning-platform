using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

public sealed class IndexCreationService(MongoDbContext context) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var keys = Builders<Review>.IndexKeys;

        await context.Reviews.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<Review>(
                    keys.Ascending(r => r.CourseId).Descending(r => r.CreatedAt),
                    new CreateIndexOptions { Name = "ix_reviews_course_created" }),
                new CreateIndexModel<Review>(
                    keys.Text(r => r.Text),
                    new CreateIndexOptions { Name = "ix_reviews_text" }),
                new CreateIndexModel<Review>(
                    keys.Ascending(r => r.CourseId).Ascending(r => r.Author.UserId),
                    new CreateIndexOptions { Name = "ux_reviews_course_author", Unique = true })
            },
            ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
