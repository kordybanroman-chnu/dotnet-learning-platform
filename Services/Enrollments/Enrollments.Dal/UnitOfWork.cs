using System.Data;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal;

public interface IUnitOfWork : IAsyncDisposable
{
    IStudentRepository Students { get; }
    IEnrollmentRepository Enrollments { get; }
    ICourseRepository Courses { get; }
    Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    public UnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
        Students = new StudentRepository(_connection, () => _transaction);
        Enrollments = new EnrollmentRepository(_connection, () => _transaction);
        Courses = new CourseRepository(_connection, () => _transaction);
    }

    public IStudentRepository Students { get; }
    public IEnrollmentRepository Enrollments { get; }
    public ICourseRepository Courses { get; }

    public async Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(level, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _transaction!.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
