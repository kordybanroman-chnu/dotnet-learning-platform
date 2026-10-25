using AutoMapper;
using Catalog.Dal;
using Catalog.Domain;

namespace Catalog.Bll;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryWithCoursesDto> GetByIdAsync(long id, CancellationToken ct = default);
}

public sealed class CategoryService(IUnitOfWork uow, IMapper mapper) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
        => mapper.Map<IReadOnlyList<CategoryDto>>(await uow.Categories.GetAllAsync(ct));

    public async Task<CategoryWithCoursesDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetWithCoursesAsync(id, ct)
            ?? throw new NotFoundException($"Category {id} not found");
        return mapper.Map<CategoryWithCoursesDto>(category);
    }
}
