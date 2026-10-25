using Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

public static class DbInitializer
{
    public static async Task SeedAsync(CatalogDbContext db, CancellationToken ct = default)
    {
        if (await db.Courses.AnyAsync(ct))
            return;

        var csharp = CreateCourse("csharp-basics", "Основи C#", 1200m, 1,
            "Синтаксис, ООП, LINQ та робота з колекціями.", 20, new long[] { 1 },
            "/img/csharp-basics.jpg");
        var aspnet = CreateCourse("aspnet-core-api", "ASP.NET Core API", 1800m, 1,
            "REST, контролери, валідація, обробка помилок і EF Core.", 24, new long[] { 1, 3 },
            "/img/aspnet-core-api.jpg");
        var mongo = CreateCourse("mongodb-dotnet", "MongoDB для .NET", 1500m, 2,
            "Документна модель, індекси, aggregation pipeline і драйвер .NET.", 16, new long[] { 2 },
            "/img/mongodb-dotnet.jpg");

        db.Courses.AddRange(csharp, aspnet, mongo);
        await db.SaveChangesAsync(ct);
    }

    private static Course CreateCourse(
        string slug, string title, decimal price, long instructorId,
        string syllabus, int durationHours, long[] categoryIds, string imageUrl)
    {
        var course = new Course(slug, title, price, instructorId);
        course.SetDetail(syllabus, durationHours);
        foreach (var categoryId in categoryIds)
            course.AttachToCategory(categoryId);
        course.AddImage(imageUrl, 0);
        return course;
    }
}
