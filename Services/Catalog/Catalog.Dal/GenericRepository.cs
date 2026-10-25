using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

public class GenericRepository<T>(CatalogDbContext context) : IGenericRepository<T> where T : class
{
    protected CatalogDbContext Context { get; } = context;
    protected DbSet<T> Set { get; } = context.Set<T>();

    public async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
        => await Set.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);

    public void Delete(T entity) => Set.Remove(entity);
}
