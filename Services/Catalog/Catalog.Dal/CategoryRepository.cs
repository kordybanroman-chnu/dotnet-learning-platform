using Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

public sealed class CategoryRepository(CatalogDbContext context)
    : GenericRepository<Category>(context), ICategoryRepository
{
    public async Task<Category?> GetWithCoursesAsync(long id, CancellationToken ct = default)
    {
        var category = await Set.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return null;

        await Context.Entry(category)
            .Collection(c => c.CourseCategories)
            .Query()
            .Include(pc => pc.Course)
            .LoadAsync(ct);

        return category;
    }

    public async Task<IReadOnlyList<long>> GetMissingIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return Array.Empty<long>();

        var existing = await Set.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);

        return ids.Except(existing).ToList();
    }
}
