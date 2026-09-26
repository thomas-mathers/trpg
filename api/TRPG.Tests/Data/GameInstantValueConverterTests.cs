using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Domain;

namespace TRPG.Tests.Data;

public sealed class GameInstantValueConverterTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Converter_RoundTripsGameInstant_AsTimestampWithoutTimeZone()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE game_instant_test_rows (
                id uuid PRIMARY KEY,
                instant timestamp without time zone NOT NULL
            )
            """,
            TestContext.Current.CancellationToken
        );
        var expected = new GameInstant(
            new DateTime(975, 8, 17, 14, 23, 51, DateTimeKind.Unspecified)
        );
        context.Rows.Add(new GameInstantTestRow { Instant = expected });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();
        var persisted = await context.Rows.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, persisted.Instant);
        Assert.Equal(DateTimeKind.Unspecified, persisted.Instant.Value.Kind);
        Assert.Equal(
            "timestamp without time zone",
            context
                .Model.FindEntityType(typeof(GameInstantTestRow))!
                .FindProperty(nameof(GameInstantTestRow.Instant))!
                .GetColumnType()
        );
    }

    private GameInstantTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GameInstantTestDbContext>()
            .UseNpgsql(database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new GameInstantTestDbContext(options);
    }

    private sealed class GameInstantTestDbContext(
        DbContextOptions<GameInstantTestDbContext> options
    ) : DbContext(options)
    {
        public DbSet<GameInstantTestRow> Rows => Set<GameInstantTestRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GameInstantTestRow>(entity =>
            {
                entity.ToTable("game_instant_test_rows");
                entity
                    .Property(row => row.Instant)
                    .HasConversion<GameInstantValueConverter>()
                    .HasColumnType("timestamp without time zone");
            });
        }
    }

    private sealed class GameInstantTestRow
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public required GameInstant Instant { get; init; }
    }
}
