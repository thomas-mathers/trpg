using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using TRPG.Data;

[assembly: AssemblyFixture(typeof(TRPG.Tests.PostgreSqlFixture))]
[assembly: Xunit.v3.Parallelization(MaxThreads = 8)]

namespace TRPG.Tests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    internal const string TemplateDatabase = "trpg_template";

    internal string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {TemplateDatabase}";
        await command.ExecuteNonQueryAsync();

        var options = new DbContextOptionsBuilder<TrpgDbContext>()
            .UseNpgsql(
                new NpgsqlConnectionStringBuilder(ConnectionString)
                {
                    Database = TemplateDatabase,
                    Pooling = false,
                }.ConnectionString
            )
            .Options;
        await using var context = new TrpgDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
