using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

[Collection("Database")]
public sealed class SyncCreatureSpawnerCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncCreatureSpawnerCommandHandler _handler = null!;
    private readonly Location _location = Builders.MakeLocation();
    private Guid _worldId => _location.WorldId;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SyncCreatureSpawnerCommandHandler>();

        _context.Locations.Add(_location);
        _context.Factions.Add(Builders.MakeFaction(_worldId, creatureType: CreatureType.Beast));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoSpawnerExistsAtTheLocation()
    {
        // Act
        await _handler.Handle(
            new SyncCreatureSpawnerCommand
            {
                LocationId = _location.Id,
                PlayerLevel = 1,
                CurrentPlaytime = TimeSpan.FromHours(2),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext
                .Creatures.Where(c => c.LocationId == _location.Id)
                .AnyAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_SpawnsCreatures_WhenScheduleHasTriggeredAndPopulationIsBelowMax()
    {
        // Arrange
        var spawner = Builders.MakeCreatureSpawner(_worldId, _location.Id, maxPopulation: 2);
        _context.CreatureSpawners.Add(spawner);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — a full in-game day has passed, enough to trigger a daily-at-hour-0 schedule
        await _handler.Handle(
            new SyncCreatureSpawnerCommand
            {
                LocationId = _location.Id,
                PlayerLevel = 1,
                CurrentPlaytime = TimeSpan.FromHours(2),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var spawned = await verifyContext
            .Creatures.Where(c => c.SpawnerId == spawner.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, spawned.Count);
    }

    [Fact]
    public async Task Handle_AdvancesLastSyncPlaytime_AfterSpawning()
    {
        // Arrange
        var spawner = Builders.MakeCreatureSpawner(_worldId, _location.Id, maxPopulation: 1);
        _context.CreatureSpawners.Add(spawner);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var currentPlaytime = TimeSpan.FromHours(2);

        // Act
        await _handler.Handle(
            new SyncCreatureSpawnerCommand
            {
                LocationId = _location.Id,
                PlayerLevel = 1,
                CurrentPlaytime = currentPlaytime,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedSpawner = await verifyContext.CreatureSpawners.SingleAsync(
            s => s.Id == spawner.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(currentPlaytime, updatedSpawner.LastSyncPlaytime);
    }

    [Fact]
    public async Task Handle_DoesNotSpawn_WhenScheduleHasNotYetTriggered()
    {
        // Arrange
        var spawner = Builders.MakeCreatureSpawner(_worldId, _location.Id, maxPopulation: 2);
        _context.CreatureSpawners.Add(spawner);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — only half an in-game day has passed
        await _handler.Handle(
            new SyncCreatureSpawnerCommand
            {
                LocationId = _location.Id,
                PlayerLevel = 1,
                CurrentPlaytime = TimeSpan.FromHours(1),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext
                .Creatures.Where(c => c.LocationId == _location.Id)
                .AnyAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_DoesNotSpawn_WhenPopulationIsAlreadyAtMax()
    {
        // Arrange
        var spawner = Builders.MakeCreatureSpawner(_worldId, _location.Id, maxPopulation: 1);
        _context.CreatureSpawners.Add(spawner);
        _context.Creatures.Add(
            Builders.MakeCreature(_worldId, locationId: _location.Id, spawnerId: spawner.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SyncCreatureSpawnerCommand
            {
                LocationId = _location.Id,
                PlayerLevel = 1,
                CurrentPlaytime = TimeSpan.FromHours(2),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var spawnedCount = await verifyContext.Creatures.CountAsync(
            c => c.SpawnerId == spawner.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, spawnedCount);
    }
}
