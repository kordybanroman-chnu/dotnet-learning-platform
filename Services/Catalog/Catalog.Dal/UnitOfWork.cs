using Catalog.Domain;

namespace Catalog.Dal;

public interface IUnitOfWork
{
    ICourseRepository Courses { get; }
    ICategoryRepository Categories { get; }
    ICourseCategoryRepository CourseCategories { get; }
    IGenericRepository<Instructor> Instructors { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CatalogDbContext _context;

    public UnitOfWork(CatalogDbContext context)
    {
        _context = context;
        Courses = new CourseRepository(context);
        Categories = new CategoryRepository(context);
        CourseCategories = new CourseCategoryRepository(context);
        Instructors = new GenericRepository<Instructor>(context);
    }

    public ICourseRepository Courses { get; }
    public ICategoryRepository Categories { get; }
    public ICourseCategoryRepository CourseCategories { get; }
    public IGenericRepository<Instructor> Instructors { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
