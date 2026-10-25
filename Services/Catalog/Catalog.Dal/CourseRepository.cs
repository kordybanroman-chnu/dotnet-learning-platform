using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

public sealed class CourseRepository(CatalogDbContext context)
    : GenericRepository<Course>(context), ICourseRepository
{
    public Task<Course?> GetWithDetailsAsync(long id, CancellationToken ct = default)
        => Set.Include(c => c.Instructor)
            .Include(c => c.Detail)
            .Include(c => c.Images)
            .Include(c => c.CourseCategories).ThenInclude(pc => pc.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default)
        => Set.AnyAsync(c => c.Slug == slug, ct);

    public async Task<IReadOnlyList<Course>> ListAsync(ISpecification<Course> spec, CancellationToken ct = default)
        => await Set.WithSpecification(spec).AsNoTracking().ToListAsync(ct);

    public Task<int> CountAsync(ISpecification<Course> spec, CancellationToken ct = default)
        => Set.WithSpecification(spec).AsNoTracking().CountAsync(ct);
}
