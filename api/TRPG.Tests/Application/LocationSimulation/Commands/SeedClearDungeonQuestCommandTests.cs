using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SeedClearDungeonQuestCommandTests
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly DatabaseFixture _database;
    private readonly Location _giverLocation;
    private readonly Creature _giver;
    private readonly Location _dungeonExteriorLocation;
    private readonly Building _dungeon;
    private readonly Location _dungeonRoomLocation;
    private readonly Room _dungeonRoom;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private ICommandHandler<SeedClearDungeonQuestCommand, bool> _handler = null!;

    public SeedClearDungeonQuestCommandTests(DatabaseFixture database)
    {
        _database = database;
        _giverLocation = Builders.MakeLocation(_worldId, _stateId);
        _giver = Builders.MakeCreature(_worldId, locationId: _giverLocation.Id, name: "Giver");
        _dungeonExteriorLocation = Builders.MakeLocation(_worldId, _stateId);
        _dungeon = Builders.MakeBuilding(
            exteriorLocationId: _dungeonExteriorLocation.Id,
            worldId: _worldId,
            buildingType: BuildingType.Cave
        );
        var dungeonRoomId = Guid.NewGuid();
        _dungeonRoomLocation = Builders.MakeLocation(_worldId, _stateId, roomId: dungeonRoomId);
        _dungeonRoom = Builders.MakeRoom(
            _dungeon.Id,
            id: dungeonRoomId,
            worldId: _worldId,
            locationId: _dungeonRoomLocation.Id
        );
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<
            ICommandHandler<SeedClearDungeonQuestCommand, bool>
        >();

        _context.Locations.AddRange(_giverLocation, _dungeonExteriorLocation, _dungeonRoomLocation);
        _context.Creatures.Add(_giver);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(_giver.Id, locationId: _giverLocation.Id, worldId: _worldId)
        );
        _context.Buildings.Add(_dungeon);
        _context.Rooms.Add(_dungeonRoom);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task SeedLivingHostiles(int count)
    {
        var group = Builders.MakeEncounterGroup(_worldId, _dungeonRoomLocation.Id, Guid.NewGuid());
        _context.EncounterGroups.Add(group);
        for (var i = 0; i < count; i++)
        {
            var monster = Builders.MakeCreature(
                _worldId,
                creatureType: CreatureType.Beast,
                locationId: _dungeonRoomLocation.Id
            );
            _context.Creatures.Add(monster);
            _context.EncounterGroupMembers.Add(
                Builders.MakeEncounterGroupMember(_worldId, group.Id, monster.Id)
            );
        }
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_OffersAQuestToClearIt_WhenAnUnclearedDungeonExistsInState()
    {
        // Arrange
        await SeedLivingHostiles(3);

        // Act
        var result = await _handler.Handle(
            new SeedClearDungeonQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _giverLocation.Id,
                PlayerLevel = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result);
        var quest = await _context.Quests.SingleAsync(
            q => q.WorldId == _worldId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_giver.Id, quest.GiverId);
        var objective = await _context
            .QuestObjectives.OfType<ClearLocationObjective>()
            .SingleAsync(o => o.QuestId == quest.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_dungeon.Id, objective.BuildingId);
        Assert.Equal(3, objective.RequiredAmount);
    }

    [Fact]
    public async Task Handle_SkipsAnAlreadyClearDungeon_AndOffersTheUnclearedOneInstead()
    {
        // Arrange — a second, already-cleared dungeon in the same state with no living hostiles
        var clearedDungeonExteriorLocation = Builders.MakeLocation(_worldId, _stateId);
        var clearedDungeon = Builders.MakeBuilding(
            exteriorLocationId: clearedDungeonExteriorLocation.Id,
            worldId: _worldId,
            buildingType: BuildingType.Cave
        );
        var clearedRoomId = Guid.NewGuid();
        var clearedRoomLocation = Builders.MakeLocation(_worldId, _stateId, roomId: clearedRoomId);
        var clearedRoom = Builders.MakeRoom(
            clearedDungeon.Id,
            id: clearedRoomId,
            worldId: _worldId,
            locationId: clearedRoomLocation.Id
        );
        _context.Locations.AddRange(clearedDungeonExteriorLocation, clearedRoomLocation);
        _context.Buildings.Add(clearedDungeon);
        _context.Rooms.Add(clearedRoom);
        await SeedLivingHostiles(2);

        // Act
        var result = await _handler.Handle(
            new SeedClearDungeonQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _giverLocation.Id,
                PlayerLevel = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result);
        var objective = await _context
            .QuestObjectives.OfType<ClearLocationObjective>()
            .SingleAsync(o => o.BuildingId == _dungeon.Id, TestContext.Current.CancellationToken);
        Assert.Equal(2, objective.RequiredAmount);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNoGiverCandidateIsAtTheLocation()
    {
        // Arrange
        await SeedLivingHostiles(1);

        // Act
        var result = await _handler.Handle(
            new SeedClearDungeonQuestCommand
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
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNoDungeonInTheStateHasAnyLivingHostiles()
    {
        // Act — no hostiles seeded, so the only dungeon in-state is already clear
        var result = await _handler.Handle(
            new SeedClearDungeonQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _giverLocation.Id,
                PlayerLevel = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenThePlayerAlreadyHasAnActiveClearQuestForTheOnlyDungeon()
    {
        // Arrange
        await SeedLivingHostiles(2);
        var playerId = Guid.NewGuid();
        var existingQuest = Builders.MakeQuest(_giver.Id, _worldId);
        var existingObjective = new ClearLocationObjective
        {
            WorldId = _worldId,
            QuestId = existingQuest.Id,
            BuildingId = _dungeon.Id,
            RequiredAmount = 1,
        };
        _context.Quests.Add(existingQuest);
        _context.QuestObjectives.Add(existingObjective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = playerId,
                QuestId = existingQuest.Id,
                Status = QuestStatus.Accepted,
                WorldId = _worldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new SeedClearDungeonQuestCommand
            {
                WorldId = _worldId,
                PlayerId = playerId,
                LocationId = _giverLocation.Id,
                PlayerLevel = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }
}
