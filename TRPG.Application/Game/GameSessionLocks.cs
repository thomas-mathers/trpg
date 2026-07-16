using Microsoft.EntityFrameworkCore;
using Npgsql;
using TRPG.Data;

namespace TRPG.Application.Game;

public class GameSessionLocks(TrpgDbContext context)
{
    public async Task<GameSessionLock> Acquire(
        Guid sessionId,
        CancellationToken cancellationToken = default
    )
    {
        var connectionString = context.Database.GetConnectionString();
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var lockCommand = connection.CreateCommand();
        lockCommand.CommandText = "SELECT pg_advisory_lock($1)";
        lockCommand.Parameters.AddWithValue(GameSessionLock.ToLockKey(sessionId));
        await lockCommand.ExecuteScalarAsync(cancellationToken);

        return new GameSessionLock(connection, sessionId);
    }
}
