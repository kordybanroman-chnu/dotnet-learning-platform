using Dapper;
using Enrollments.Domain;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal;

public interface ICourseRepository : IGenericRepository<Course>
{
    Task DecreaseSeatsAsync(long courseId, int units, CancellationToken ct = default);
}

public sealed class CourseRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    : BaseDapperRepository<Course>(connection, currentTransaction, "dbo.Courses"), ICourseRepository
{
    public override async Task<Course?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        return await Connection.QuerySingleOrDefaultAsync<Course>(new CommandDefinition(
            "SELECT Id, Title, Price, SeatsAvailable FROM dbo.Courses WHERE Id = @Id",
            new { Id = id }, Transaction, cancellationToken: ct));
    }

    public override async Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<Course>(new CommandDefinition(
            "SELECT Id, Title, Price, SeatsAvailable FROM dbo.Courses ORDER BY Id",
            Transaction, cancellationToken: ct));
        return rows.AsList();
    }

    public override Task<long> AddAsync(Course entity, CancellationToken ct = default)
        => throw new BusinessConflictException("Courses are reference data owned by Catalog and cannot be created here");

    public override Task<bool> DeleteAsync(long id, CancellationToken ct = default)
        => throw new BusinessConflictException("Courses are reference data owned by Catalog and cannot be deleted here");

    public async Task DecreaseSeatsAsync(long courseId, int units, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            var affected = await Connection.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.Courses SET SeatsAvailable = SeatsAvailable - @Units WHERE Id = @Id AND SeatsAvailable >= @Units",
                new { Id = courseId, Units = units }, Transaction, cancellationToken: ct));
            if (affected == 0)
                throw new BusinessConflictException($"Not enough seats for course {courseId}");
        }
        catch (SqlException ex)
        {
            throw Map(ex);
        }
    }
}
