using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.Quests.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SeedCaptiveRescueQuestCommandTests
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    // Instance, not static — each test seeds its own Faction keyed only by CreatureType, and the
    // class shares one database across tests, so a shared world id would let those factions collide.
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _cityId = Guid.NewGuid();
    private readonly DatabaseFixture _database;
    private readonly Location _entranceLocation;
    private readonly Location _giverLocation;
    private readonly Creature _giver;
    private readonly Creature _captive;
    private readonly Building _building;
    private readonly Location _cellBlockLocation;
    private readonly Room _cellBlockRoom;
    private readonly Faction _goblinFaction;
    private readonly Faction _demonFaction;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private ICommandHandler<SeedCaptiveRescueQuestCommand, bool> _handler = null!;

    public SeedCaptiveRescueQuestCommandTests(DatabaseFixture database)
    {
        _database = database;
        _entranceLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _cityId);
        // The giver works elsewhere in the city, not at the seed/entrance location itself —
        // giver selection is city-wide, not tied to where the seed check happens.
        _giverLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _cityId);
        _giver = Builders.MakeCreature(_worldId, locationId: _giverLocation.Id, name: "Giver");
        _captive = Builders.MakeCreature(_worldId, name: "Captive");
        _building = Builders.MakeBuilding(worldId: _worldId, buildingType: BuildingType.Crypt);
        _cellBlockLocation = Builders.MakeLocation(_worldId, _stateId);
        _cellBlockRoom = Builders.MakeRoom(
            _building.Id,
            worldId: _worldId,
            locationId: _cellBlockLocation.Id,
            role: RoomRole.CellBlock
        );
        // The captive-rescue guard is drawn from a fixed captor pool (Goblin, Demon) independent
        // of the dungeon's own theme, so both need a faction available or CreatureSpawnFiller
        // throws on whichever one it happens to pick.
        _goblinFaction = Builders.MakeFaction(worldId: _worldId, creatureType: CreatureType.Goblin);
        _demonFaction = Builders.MakeFaction(worldId: _worldId, creatureType: CreatureType.Demon);
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<
            ICommandHandler<SeedCaptiveRescueQuestCommand, bool>
        >();

        _context.Locations.AddRange(_entranceLocation, _giverLocation, _cellBlockLocation);
        _context.Creatures.AddRange(_giver, _captive);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(_giver.Id, locationId: _giverLocation.Id, worldId: _worldId)
        );
        _context.Buildings.Add(_building);
        _context.Rooms.Add(_cellBlockRoom);
        _context.Factions.AddRange(_goblinFaction, _demonFaction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task SeedRelationship()
    {
        _context.Relationships.Add(
            Builders.MakeRelationship(_giver.Id, _captive.Id, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_CapturesTheRelativeAndOffersAQuest_WhenEverythingIsEligible()
    {
        // Arrange
        await SeedRelationship();

        // Act
        var result = await _handler.Handle(
            new SeedCaptiveRescueQuestCommand
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
        await using var verification = _database.CreateContext();
        var captive = await verification.Creatures.SingleAsync(
            creature => creature.Id == _captive.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Restrained, captive.State);
        Assert.Equal(_cellBlockLocation.Id, captive.LocationId);

        var cell = await verification
            .Props.OfType<Cell>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(_cellBlockLocation.Id, cell.LocationId);
        Assert.Equal(_captive.Id, cell.CreatureId);
        Assert.True(cell.IsLocked);

        var quest = await verification.Quests.SingleAsync(
            q => q.WorldId == _worldId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_giver.Id, quest.GiverId);

        var objective = await verification
            .QuestObjectives.OfType<FreeCreatureObjective>()
            .SingleAsync(o => o.WorldId == _worldId, TestContext.Current.CancellationToken);
        Assert.Equal(quest.Id, objective.QuestId);
        Assert.Equal(_captive.Id, objective.CreatureId);

        var factionIds = new[] { _goblinFaction.Id, _demonFaction.Id };
        Assert.True(
            await verification.EncounterGroups.AnyAsync(
                group =>
                    group.LocationId == _cellBlockLocation.Id
                    && factionIds.AsEnumerable().Contains(group.FactionId),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenGiverHasNoEligibleRelative()
    {
        // Act
        var result = await _handler.Handle(
            new SeedCaptiveRescueQuestCommand
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
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNoCellBlockRoomExistsInTheState()
    {
        // Arrange
        await SeedRelationship();
        await _context
            .Rooms.Where(room => room.Id == _cellBlockRoom.Id)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new SeedCaptiveRescueQuestCommand
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
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenTheRelativeIsAlreadyPartOfAnotherRescueQuest()
    {
        // Arrange
        await SeedRelationship();
        var existingQuest = Builders.MakeQuest(Guid.NewGuid(), _worldId);
        _context.Quests.Add(existingQuest);
        _context.QuestObjectives.Add(
            Builders.MakeFreeCreatureObjective(existingQuest.Id, _captive.Id, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new SeedCaptiveRescueQuestCommand
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
    }
}
