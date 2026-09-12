using Microsoft.EntityFrameworkCore;
using Npgsql;
using TRPG.Data;

namespace TRPG.Tests;

public sealed class DatabaseFixture(PostgreSqlFixture postgres) : IAsyncLifetime
{
    private readonly string _databaseName = $"test_{Guid.NewGuid():N}";

    internal string ConnectionString =>
        new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Database = _databaseName,
            MaxPoolSize = 8,
        }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"CREATE DATABASE {_databaseName} TEMPLATE {PostgreSqlFixture.TemplateDatabase}";
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        using var poolConnection = new NpgsqlConnection(ConnectionString);
        NpgsqlConnection.ClearPool(poolConnection);
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    internal TrpgDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TrpgDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new TrpgDbContext(options);
    }
}
