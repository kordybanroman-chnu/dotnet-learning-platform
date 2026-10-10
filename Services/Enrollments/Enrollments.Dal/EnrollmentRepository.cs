using System.Data;
using Dapper;
using Enrollments.Domain;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal;

public interface IEnrollmentRepository : IGenericRepository<Enrollment>
{
    Task<Enrollment?> GetWithItemsAsync(long enrollmentId, CancellationToken ct = default);
    Task<IReadOnlyList<Enrollment>> GetByStudentAsync(long studentId, CancellationToken ct = default);
    Task AddItemAsync(long enrollmentId, long courseId, int units, CancellationToken ct = default);
    Task ConfirmAsync(long enrollmentId, CancellationToken ct = default);
    Task CancelAsync(long enrollmentId, CancellationToken ct = default);
}

public sealed class EnrollmentRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    : BaseDapperRepository<Enrollment>(connection, currentTransaction, "dbo.Enrollments"), IEnrollmentRepository
{
    public override async Task<Enrollment?> GetByIdAsync(long id, CancellationToken ct = default)
        => await GetWithItemsAsync(id, ct);

    public async Task<Enrollment?> GetWithItemsAsync(long enrollmentId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT e.Id, e.StudentId, e.Status, e.EnrolledAt, d.Note, d.PreferredSchedule,
                   i.EnrollmentId, i.CourseId, i.CourseTitle, i.UnitPrice, i.Units
            FROM dbo.Enrollments e
            LEFT JOIN dbo.EnrollmentDetails d ON d.EnrollmentId = e.Id
            LEFT JOIN dbo.EnrollmentItems i ON i.EnrollmentId = e.Id
            WHERE e.Id = @EnrollmentId AND e.IsDeleted = 0
            """;
        await EnsureOpenAsync(ct);
        Enrollment? result = null;
        await Connection.QueryAsync<Enrollment, EnrollmentItem?, Enrollment>(
            new CommandDefinition(sql, new { EnrollmentId = enrollmentId }, Transaction, cancellationToken: ct),
            (enrollment, item) =>
            {
                result ??= enrollment;
                if (item is not null)
                    result.Items.Add(item);
                return result;
            },
            splitOn: "EnrollmentId");
        return result;
    }

    public async Task<IReadOnlyList<Enrollment>> GetByStudentAsync(long studentId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, StudentId, Status, EnrolledAt
            FROM dbo.Enrollments WHERE StudentId = @StudentId AND IsDeleted = 0 ORDER BY Id
            """;
        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<Enrollment>(new CommandDefinition(
            sql, new { StudentId = studentId }, Transaction, cancellationToken: ct));
        return rows.AsList();
    }

    public override async Task<long> AddAsync(Enrollment entity, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            return await Connection.QuerySingleAsync<long>(new CommandDefinition(
                "dbo.usp_CreateEnrollment",
                new { StudentId = entity.StudentId, Note = entity.Note, PreferredSchedule = entity.PreferredSchedule },
                Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        }
        catch (SqlException ex)
        {
            throw Map(ex);
        }
    }

    public async Task AddItemAsync(long enrollmentId, long courseId, int units, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(new CommandDefinition(
                "dbo.usp_AddEnrollmentItem",
                new { EnrollmentId = enrollmentId, CourseId = courseId, Units = units },
                Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        }
        catch (SqlException ex)
        {
            throw Map(ex);
        }
    }

    public async Task ConfirmAsync(long enrollmentId, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(new CommandDefinition(
                "dbo.usp_ConfirmEnrollment",
                new { EnrollmentId = enrollmentId },
                Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        }
        catch (SqlException ex)
        {
            throw Map(ex);
        }
    }

    public async Task CancelAsync(long enrollmentId, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(new CommandDefinition(
                "dbo.usp_CancelEnrollment",
                new { EnrollmentId = enrollmentId },
                Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        }
        catch (SqlException ex)
        {
            throw Map(ex);
        }
    }
}
