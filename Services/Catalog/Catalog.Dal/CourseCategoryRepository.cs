using Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

public sealed class CourseCategoryRepository(CatalogDbContext context)
    : GenericRepository<CourseCategory>(context), ICourseCategoryRepository
{
    public async Task<IReadOnlyList<Course>> GetRecentlyAddedAsync(
        string categorySlug, DateTime since, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(pc => pc.Category.Slug == categorySlug && pc.AddedAt >= since)
            .OrderByDescending(pc => pc.AddedAt)
            .Select(pc => pc.Course)
            .ToListAsync(ct);
}
