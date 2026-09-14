using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SeedFetchQuestCommandTests : IAsyncLifetime, IClassFixture<DatabaseFixture>
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
    private ICommandHandler<SeedFetchQuestCommand, bool> _handler = null!;

    public SeedFetchQuestCommandTests(DatabaseFixture database)
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
        _handler = _services.GetRequiredService<ICommandHandler<SeedFetchQuestCommand, bool>>();

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

    private async Task<Guid[]> SeedLivingHostiles(int count)
    {
        var group = Builders.MakeEncounterGroup(_worldId, _dungeonRoomLocation.Id, Guid.NewGuid());
        _context.EncounterGroups.Add(group);
        var monsterIds = new Guid[count];
        for (var i = 0; i < count; i++)
        {
            var monster = Builders.MakeCreature(
                _worldId,
                creatureType: CreatureType.Beast,
                locationId: _dungeonRoomLocation.Id
            );
            monsterIds[i] = monster.Id;
            _context.Creatures.Add(monster);
            _context.EncounterGroupMembers.Add(
                Builders.MakeEncounterGroupMember(_worldId, group.Id, monster.Id)
            );
        }
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return monsterIds;
    }

    [Fact]
    public async Task Handle_OffersAFetchQuest_WhenTheDungeonHasEnoughLivingHostiles()
    {
        // Arrange
        var monsterIds = await SeedLivingHostiles(3);

        // Act
        var result = await _handler.Handle(
            new SeedFetchQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _giverLocation.Id,
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
            .QuestObjectives.OfType<GiveItemsObjective>()
            .SingleAsync(o => o.QuestId == quest.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_giver.Id, objective.RecipientId);
        Assert.Equal(3, objective.ItemIds.Count);
        Assert.Equal(3, objective.RequiredAmount);
        var trophies = await _context
            .Items.Where(item => objective.ItemIds.Contains(item.Id))
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, trophies.Length);
        Assert.All(trophies, trophy => Assert.Contains(trophy.Ownership.OwnerId, monsterIds));
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNoGiverCandidateIsAtTheLocation()
    {
        // Arrange
        await SeedLivingHostiles(3);

        // Act
        var result = await _handler.Handle(
            new SeedFetchQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNoDungeonHasEnoughLivingHostiles()
    {
        // Arrange
        await SeedLivingHostiles(2);

        // Act
        var result = await _handler.Handle(
            new SeedFetchQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _giverLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenThePlayerAlreadyHasAnActiveFetchQuestFromTheGiver()
    {
        // Arrange
        await SeedLivingHostiles(3);
        var playerId = Guid.NewGuid();
        var existingQuest = Builders.MakeQuest(_giver.Id, _worldId);
        var existingObjective = new GiveItemsObjective
        {
            WorldId = _worldId,
            QuestId = existingQuest.Id,
            ItemIds = [Guid.NewGuid()],
            RecipientId = _giver.Id,
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
            new SeedFetchQuestCommand
            {
                WorldId = _worldId,
                PlayerId = playerId,
                LocationId = _giverLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }
}
