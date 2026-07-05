using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TRPG.Data;

namespace TRPG.Tests;

public sealed class DatabaseFixture : IAsyncLifetime {
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .Build();

    public async ValueTask InitializeAsync() {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() {
        await _container.DisposeAsync();
    }

    internal string ConnectionString => _container.GetConnectionString();

    internal TrpgDbContext CreateContext() {
        var options = new DbContextOptionsBuilder<TrpgDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new TrpgDbContext(options);
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;