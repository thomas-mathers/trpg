using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TRPG.Data;

namespace TRPG.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .Build();

    internal TrpgDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TrpgDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new TrpgDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
        => await _container.DisposeAsync();
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
