using Npgsql;

namespace TRPG.Application.Game;

public sealed class GameSessionLock(NpgsqlConnection connection, Guid sessionId) : IAsyncDisposable
{
    internal NpgsqlConnection Connection { get; } = connection;
    public Guid SessionId { get; } = sessionId;
    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await using var unlockCommand = Connection.CreateCommand();
        unlockCommand.CommandText = "SELECT pg_advisory_unlock($1)";
        unlockCommand.Parameters.AddWithValue(ToLockKey(SessionId));
        await unlockCommand.ExecuteScalarAsync();
        await Connection.DisposeAsync();
    }

    internal static long ToLockKey(Guid sessionId) =>
        BitConverter.ToInt64(sessionId.ToByteArray(), 0);
}
