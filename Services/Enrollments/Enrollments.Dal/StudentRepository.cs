using System.Data;
using Enrollments.Domain;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<Student>> GetAllAsync(CancellationToken ct = default);
    Task<long> AddAsync(Student entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class StudentRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    : RepositoryBase(connection, currentTransaction), IStudentRepository
{
    public async Task<Student?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        const string sql = "SELECT Id, Email, FullName FROM dbo.Students WHERE Id = @Id AND IsDeleted = 0";
        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        return new Student
        {
            Id = reader.GetInt64(0),
            Email = reader.GetString(1),
            FullName = reader.GetString(2),
        };
    }

    public async Task<IReadOnlyList<Student>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT Id, Email, FullName FROM dbo.Students WHERE IsDeleted = 0 ORDER BY Id";
        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<Student>();
        while (await reader.ReadAsync(ct))
            result.Add(new Student
            {
                Id = reader.GetInt64(0),
                Email = reader.GetString(1),
                FullName = reader.GetString(2),
            });
        return result;
    }

    public async Task<long> AddAsync(Student entity, CancellationToken ct = default)
    {
        const string sql = "INSERT INTO dbo.Students (Email, FullName) OUTPUT INSERTED.Id VALUES (@Email, @FullName)";
        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = entity.Email;
        command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = entity.FullName;
        try
        {
            return (long)await command.ExecuteScalarAsync(ct)!;
        }
        catch (SqlException ex)
        {
            throw Map(ex);
        }
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        const string sql = "UPDATE dbo.Students SET IsDeleted = 1 WHERE Id = @Id AND IsDeleted = 0";
        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }
}
