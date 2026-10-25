using Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

public interface ICourseCategoryRepository : IGenericRepository<CourseCategory>
{
    Task<IReadOnlyList<Course>> GetRecentlyAddedAsync(
        string categorySlug, DateTime since, CancellationToken ct = default);
}
