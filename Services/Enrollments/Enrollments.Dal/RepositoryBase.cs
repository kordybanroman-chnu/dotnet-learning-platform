using System.Data;
using Dapper;
using Enrollments.Domain;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal;

public abstract class RepositoryBase(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
{
    protected readonly SqlConnection Connection = connection;
    private readonly Func<SqlTransaction?> _currentTransaction = currentTransaction;

    protected SqlTransaction? Transaction => _currentTransaction();

    protected async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(ct);
    }

    protected static Exception Map(SqlException ex) => ex.Number switch
    {
        50001 => new NotFoundException(ex.Message),
        50002 or 50003 => new BusinessConflictException(ex.Message),
        2601 or 2627 => new BusinessConflictException("Record with the same key already exists"),
        _ => ex,
    };
}

public abstract class BaseDapperRepository<T>(SqlConnection connection, Func<SqlTransaction?> currentTransaction, string table, string keyColumn = "Id")
    : RepositoryBase(connection, currentTransaction), IGenericRepository<T> where T : class
{
    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        return await Connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(
            $"SELECT * FROM {table} WHERE {keyColumn} = @Id AND IsDeleted = 0",
            new { Id = id }, Transaction, cancellationToken: ct));
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<T>(new CommandDefinition(
            $"SELECT * FROM {table} WHERE IsDeleted = 0",
            Transaction, cancellationToken: ct));
        return rows.AsList();
    }

    public abstract Task<long> AddAsync(T entity, CancellationToken ct = default);

    public virtual async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var affected = await Connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE {table} SET IsDeleted = 1 WHERE {keyColumn} = @Id AND IsDeleted = 0",
            new { Id = id }, Transaction, cancellationToken: ct));
        return affected > 0;
    }
}
