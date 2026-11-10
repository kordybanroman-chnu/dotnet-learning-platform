using AutoMapper;
using Catalog.Bll.Caching;
using Catalog.Dal;
using Catalog.Domain;

namespace Catalog.Bll;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryWithCoursesDto> GetByIdAsync(long id, CancellationToken ct = default);
}

public sealed class CategoryService(IUnitOfWork uow, IMapper mapper, ICatalogCache cache) : ICategoryService
{
    public Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync(
            CacheKeys.Categories,
            async () => mapper.Map<IReadOnlyList<CategoryDto>>(await uow.Categories.GetAllAsync(ct)),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30),
            ct);

    public async Task<CategoryWithCoursesDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetWithCoursesAsync(id, ct)
            ?? throw new NotFoundException($"Category {id} not found");
        return mapper.Map<CategoryWithCoursesDto>(category);
    }
}
