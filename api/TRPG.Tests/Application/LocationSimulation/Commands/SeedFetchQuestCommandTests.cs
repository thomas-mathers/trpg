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
    private readonly Guid _cityId = Guid.NewGuid();
    private readonly DatabaseFixture _database;
    private readonly Location _entranceLocation;
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
        _entranceLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _cityId);
        // The giver works elsewhere in the city, not at the seed/entrance location itself, and
        // is a crafting profession the fetch-quest giver filter allows.
        _giverLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _cityId);
        _giver = Builders.MakeCreature(
            _worldId,
            profession: Profession.Alchemist,
            locationId: _giverLocation.Id,
            name: "Giver"
        );
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

        _context.Locations.AddRange(
            _entranceLocation,
            _giverLocation,
            _dungeonExteriorLocation,
            _dungeonRoomLocation
        );
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

    private Task<Guid[]> SeedLivingHostiles(int count) =>
        SeedLivingHostiles(Enumerable.Repeat(CreatureType.Beast, count).ToArray());

    private async Task<Guid[]> SeedLivingHostiles(IReadOnlyList<CreatureType> creatureTypes)
    {
        var group = Builders.MakeEncounterGroup(_worldId, _dungeonRoomLocation.Id, Guid.NewGuid());
        _context.EncounterGroups.Add(group);
        var monsterIds = new Guid[creatureTypes.Count];
        for (var i = 0; i < creatureTypes.Count; i++)
        {
            var monster = Builders.MakeCreature(
                _worldId,
                creatureType: creatureTypes[i],
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
                LocationId = _entranceLocation.Id,
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
            .QuestObjectives.OfType<GiveItemKindObjective>()
            .SingleAsync(o => o.QuestId == quest.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_giver.Id, objective.RecipientId);
        Assert.Equal("Beast Pelt", objective.ItemName);
        Assert.Equal(3, objective.RequiredAmount);
        var drops = await _context
            .Items.Where(item => item.WorldId == _worldId && item.Name == objective.ItemName)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, drops.Length);
        Assert.All(drops, drop => Assert.Contains(drop.Ownership.OwnerId, monsterIds));
    }

    [Fact]
    public async Task Handle_GroupsObjectivesByMaterialType_WhenHostilesAreOfDifferentTypes()
    {
        // Arrange
        await SeedLivingHostiles([CreatureType.Beast, CreatureType.Beast, CreatureType.Orc]);

        // Act
        var result = await _handler.Handle(
            new SeedFetchQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _entranceLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result);
        var quest = await _context.Quests.SingleAsync(
            q => q.WorldId == _worldId,
            TestContext.Current.CancellationToken
        );
        var objectives = await _context
            .QuestObjectives.OfType<GiveItemKindObjective>()
            .Where(o => o.QuestId == quest.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, objectives.Length);
        var beastObjective = Assert.Single(objectives, o => o.RequiredAmount == 2);
        Assert.Equal("Beast Pelt", beastObjective.ItemName);
        var orcObjective = Assert.Single(objectives, o => o.RequiredAmount == 1);
        Assert.Equal("Orc Tusk", orcObjective.ItemName);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenTheSeedLocationHasNoCity()
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
    public async Task Handle_ReturnsFalse_WhenTheOnlyCandidateHasADisqualifyingProfession()
    {
        // Arrange — a knight isn't a plausible gather-quest giver
        await SeedLivingHostiles(3);
        await _context
            .Creatures.Where(creature => creature.Id == _giver.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.Profession, Profession.Knight),
                TestContext.Current.CancellationToken
            );

        // Act
        var result = await _handler.Handle(
            new SeedFetchQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _entranceLocation.Id,
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
                LocationId = _entranceLocation.Id,
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
        var existingObjective = Builders.MakeGiveItemKindObjective(
            existingQuest.Id,
            "Beast Pelt",
            _giver.Id,
            worldId: _worldId
        );
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
                LocationId = _entranceLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }
}
