using Dapper;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal;

public interface IProcessedEventRepository
{
    Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct = default);
}

public sealed class ProcessedEventRepository(
    SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    : RepositoryBase(connection, currentTransaction), IProcessedEventRepository
{
    public async Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO dbo.ProcessedEvents (EventId, EventType) VALUES (@EventId, @EventType)",
                new { EventId = eventId, EventType = eventType },
                Transaction, cancellationToken: ct));
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return false;
        }
    }
}
