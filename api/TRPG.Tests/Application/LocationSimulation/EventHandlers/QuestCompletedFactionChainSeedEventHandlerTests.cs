using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.LocationSimulation.EventHandlers;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.EventHandlers;

public sealed class QuestCompletedFactionChainSeedEventHandlerTests
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
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
    private readonly Creature _player;
    private readonly Faction _faction;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private QuestCompletedFactionChainSeedEventHandler _handler = null!;
    private RecordingQuestChainGenerationScheduler _scheduler = null!;

    public QuestCompletedFactionChainSeedEventHandlerTests(DatabaseFixture database)
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
        _player = Builders.MakeCreature(
            _worldId,
            locationId: _entranceLocation.Id,
            level: 5,
            name: "Player"
        );
        _faction = Builders.MakeFaction(_worldId);
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _scheduler = new RecordingQuestChainGenerationScheduler();
        _services = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IQuestChainGenerationScheduler>(_scheduler)
            .BuildServiceProvider();
        _handler = _services.GetRequiredService<QuestCompletedFactionChainSeedEventHandler>();

        _context.Locations.AddRange(
            _entranceLocation,
            _giverLocation,
            _dungeonExteriorLocation,
            _roomLocation
        );
        _context.Creatures.AddRange(_giver, _player);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(_giver.Id, locationId: _giverLocation.Id, worldId: _worldId)
        );
        _context.Buildings.Add(_dungeon);
        _context.Rooms.Add(_room);
        _context.Factions.Add(_faction);
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
    public async Task Handle_SeedsAChainForTheJoinedFaction_WhenAnInitiationQuestCompletes()
    {
        // Arrange — giver + dungeon location + 2 hostiles = 4 entities, meeting the minimum
        await SeedLivingHostile("Wolf One");
        await SeedLivingHostile("Wolf Two");
        var quest = Builders.MakeQuest(
            _giver.Id,
            _worldId,
            name: "Initiation: The Test Faction",
            membershipRewardFactionId: _faction.Id
        );
        _context.Quests.Add(quest);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new QuestCompletedEvent(_player.Id, _worldId, quest.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var scheduled = Assert.Single(_scheduler.ScheduledCommands);
        Assert.Equal(_faction.Id, scheduled.GiverFactionId);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenTheCompletedQuestGrantsNoFactionMembership()
    {
        // Arrange
        await SeedLivingHostile("Wolf One");
        await SeedLivingHostile("Wolf Two");
        var quest = Builders.MakeQuest(_giver.Id, _worldId);
        _context.Quests.Add(quest);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new QuestCompletedEvent(_player.Id, _worldId, quest.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_scheduler.ScheduledCommands);
    }
}
