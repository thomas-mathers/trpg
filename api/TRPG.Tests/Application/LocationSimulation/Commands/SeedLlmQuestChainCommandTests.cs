using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SeedLlmQuestChainCommandTests : IAsyncLifetime, IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _cityId = Guid.NewGuid();
    private readonly DatabaseFixture _database;
    private readonly Location _entranceLocation;
    private readonly Location _giverLocation;
    private readonly Creature _giver;
    private readonly Location _dungeonExteriorLocation;
    private readonly Building _dungeon;
    private readonly Location _roomLocation;
    private readonly Room _room;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private ICommandHandler<SeedLlmQuestChainCommand, bool> _handler = null!;
    private RecordingQuestChainGenerationScheduler _scheduler = null!;

    public SeedLlmQuestChainCommandTests(DatabaseFixture database)
    {
        _database = database;
        _entranceLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _cityId);
        _giverLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _cityId);
        _giver = Builders.MakeCreature(_worldId, locationId: _giverLocation.Id, name: "Giver");
        _dungeonExteriorLocation = Builders.MakeLocation(_worldId, _stateId);
        _dungeon = Builders.MakeBuilding(
            exteriorLocationId: _dungeonExteriorLocation.Id,
            worldId: _worldId,
            buildingType: BuildingType.Cave
        );
        var roomId = Guid.NewGuid();
        _roomLocation = Builders.MakeLocation(_worldId, _stateId, roomId: roomId);
        _room = Builders.MakeRoom(
            _dungeon.Id,
            id: roomId,
            worldId: _worldId,
            locationId: _roomLocation.Id
        );
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _scheduler = new RecordingQuestChainGenerationScheduler();
        _services = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IQuestChainGenerationScheduler>(_scheduler)
            .BuildServiceProvider();
        _handler = _services.GetRequiredService<ICommandHandler<SeedLlmQuestChainCommand, bool>>();

        _context.Locations.AddRange(
            _entranceLocation,
            _giverLocation,
            _dungeonExteriorLocation,
            _roomLocation
        );
        _context.Creatures.Add(_giver);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(_giver.Id, locationId: _giverLocation.Id, worldId: _worldId)
        );
        _context.Buildings.Add(_dungeon);
        _context.Rooms.Add(_room);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task SeedLivingHostile(string name)
    {
        var group = Builders.MakeEncounterGroup(_worldId, _roomLocation.Id, Guid.NewGuid());
        var monster = Builders.MakeCreature(
            _worldId,
            creatureType: CreatureType.Beast,
            locationId: _roomLocation.Id,
            name: name
        );
        _context.EncounterGroups.Add(group);
        _context.Creatures.Add(monster);
        _context.EncounterGroupMembers.Add(
            Builders.MakeEncounterGroupMember(_worldId, group.Id, monster.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_SchedulesGeneration_WhenTheEntityPoolIsLargeEnough()
    {
        // Arrange — giver + dungeon location + 2 hostiles = 4 entities, meeting the minimum
        await SeedLivingHostile("Wolf One");
        await SeedLivingHostile("Wolf Two");

        // Act
        var result = await _handler.Handle(
            new SeedLlmQuestChainCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _entranceLocation.Id,
                PlayerLevel = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result);
        var scheduled = Assert.Single(_scheduler.ScheduledCommands);
        Assert.True(scheduled.AvailableEntities.Count >= 4);
        Assert.Contains(scheduled.AvailableEntities, entity => entity.Id == _giver.Id);
        var request = await _context.QuestChainGenerationRequests.SingleAsync(
            r => r.WorldId == _worldId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(QuestChainGenerationStatus.Pending, request.Status);
        Assert.Equal(scheduled.RequestId, request.Id);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenTheSeedLocationHasNoCity()
    {
        // Arrange
        await SeedLivingHostile("Wolf One");
        await SeedLivingHostile("Wolf Two");

        // Act
        var result = await _handler.Handle(
            new SeedLlmQuestChainCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                PlayerLevel = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
        Assert.Empty(_scheduler.ScheduledCommands);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenTheEntityPoolIsTooSparse()
    {
        // Act — no hostiles seeded, so the pool is only the giver and the dungeon location
        var result = await _handler.Handle(
            new SeedLlmQuestChainCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _entranceLocation.Id,
                PlayerLevel = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
        Assert.Empty(_scheduler.ScheduledCommands);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenARequestIsAlreadyPendingForThisPlayer()
    {
        // Arrange
        await SeedLivingHostile("Wolf One");
        await SeedLivingHostile("Wolf Two");
        var playerId = Guid.NewGuid();
        _context.QuestChainGenerationRequests.Add(
            new QuestChainGenerationRequest { WorldId = _worldId, PlayerId = playerId }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new SeedLlmQuestChainCommand
            {
                WorldId = _worldId,
                PlayerId = playerId,
                LocationId = _entranceLocation.Id,
                PlayerLevel = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
        Assert.Empty(_scheduler.ScheduledCommands);
    }
}
