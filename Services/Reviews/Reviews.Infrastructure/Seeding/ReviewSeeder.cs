using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

public sealed class ReviewSeeder(MongoDbContext context) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await context.Reviews.EstimatedDocumentCountAsync(cancellationToken: ct) > 0)
            return;

        var reviews = new[]
        {
            new Review(
                101, "Основи C#", new Author("1", "Олена"), new Rating(5),
                "Чудовий старт: багато практики й зрозумілі приклади."),
            new Review(
                101, "Основи C#", new Author("2", "Андрій"), new Rating(4),
                "Детально про LINQ, але частина прикладів застаріла."),
            new Review(
                103, "MongoDB для .NET", new Author("3", "O'Brien"), new Rating(4),
                "Корисно для переходу з SQL на документну модель.")
        };

        await context.Reviews.InsertManyAsync(reviews, cancellationToken: ct);
    }
}
