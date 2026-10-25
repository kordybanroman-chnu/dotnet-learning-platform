using Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<CourseCategory> CourseCategories => Set<CourseCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
}
