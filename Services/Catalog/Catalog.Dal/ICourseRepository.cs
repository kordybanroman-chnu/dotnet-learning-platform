using Ardalis.Specification;
using Catalog.Domain;

namespace Catalog.Dal;

public interface ICourseRepository : IGenericRepository<Course>
{
    Task<Course?> GetWithDetailsAsync(long id, CancellationToken ct = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Course>> ListAsync(ISpecification<Course> spec, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<Course> spec, CancellationToken ct = default);
}
