using Catalog.Domain;

namespace Catalog.Dal;

public interface ICategoryRepository : IGenericRepository<Category>
{
    Task<Category?> GetWithCoursesAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<long>> GetMissingIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default);
}
