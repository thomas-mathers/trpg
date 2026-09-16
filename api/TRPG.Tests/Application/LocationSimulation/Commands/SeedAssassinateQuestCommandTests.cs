using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SeedAssassinateQuestCommandTests
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
    private readonly Location _bossRoomLocation;
    private readonly Room _bossRoom;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private ICommandHandler<SeedAssassinateQuestCommand, bool> _handler = null!;

    public SeedAssassinateQuestCommandTests(DatabaseFixture database)
    {
        _database = database;
        _entranceLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _cityId);
        // The giver works elsewhere in the city, not at the seed/entrance location itself — giver
        // selection is city-wide, not tied to where the seed check happens. Profession defaults to
        // Knight, which is in the allow-list for this quest type.
        _giverLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _cityId);
        _giver = Builders.MakeCreature(_worldId, locationId: _giverLocation.Id, name: "Giver");
        _dungeonExteriorLocation = Builders.MakeLocation(_worldId, _stateId);
        _dungeon = Builders.MakeBuilding(
            exteriorLocationId: _dungeonExteriorLocation.Id,
            worldId: _worldId,
            buildingType: BuildingType.Cave
        );
        var bossRoomId = Guid.NewGuid();
        _bossRoomLocation = Builders.MakeLocation(_worldId, _stateId, roomId: bossRoomId);
        _bossRoom = Builders.MakeRoom(
            _dungeon.Id,
            id: bossRoomId,
            worldId: _worldId,
            locationId: _bossRoomLocation.Id,
            role: RoomRole.BossChamber
        );
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<
            ICommandHandler<SeedAssassinateQuestCommand, bool>
        >();

        _context.Locations.AddRange(
            _entranceLocation,
            _giverLocation,
            _dungeonExteriorLocation,
            _bossRoomLocation
        );
        _context.Creatures.Add(_giver);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(_giver.Id, locationId: _giverLocation.Id, worldId: _worldId)
        );
        _context.Buildings.Add(_dungeon);
        _context.Rooms.Add(_bossRoom);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedLivingHostile(Guid locationId, string name = "Grukk")
    {
        var group = Builders.MakeEncounterGroup(_worldId, locationId, Guid.NewGuid());
        var monster = Builders.MakeCreature(
            _worldId,
            creatureType: CreatureType.Beast,
            locationId: locationId,
            name: name
        );
        _context.EncounterGroups.Add(group);
        _context.Creatures.Add(monster);
        _context.EncounterGroupMembers.Add(
            Builders.MakeEncounterGroupMember(_worldId, group.Id, monster.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return monster;
    }

    [Fact]
    public async Task Handle_OffersAQuestToKillTheBossRoomOccupant_WhenOneExistsInState()
    {
        // Arrange
        var target = await SeedLivingHostile(_bossRoomLocation.Id);

        // Act
        var result = await _handler.Handle(
            new SeedAssassinateQuestCommand
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
        var quest = await _context.Quests.SingleAsync(
            q => q.WorldId == _worldId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_giver.Id, quest.GiverId);
        var objective = await _context
            .QuestObjectives.OfType<KillCreatureObjective>()
            .SingleAsync(o => o.QuestId == quest.Id, TestContext.Current.CancellationToken);
        Assert.Equal(target.Id, objective.CreatureId);
        await using var verifyContext = _database.CreateContext();
        var renamedTarget = await verifyContext.Creatures.FindAsync(
            [target.Id],
            TestContext.Current.CancellationToken
        );
        Assert.NotEqual("Grukk", renamedTarget!.Name);
        Assert.StartsWith("Grukk", renamedTarget.Name);
    }

    [Fact]
    public async Task Handle_IgnoresLivingHostiles_InRoomsThatAreNotTheBossChamber()
    {
        // Arrange — a hostile in an ordinary room, with no boss chamber anywhere in the dungeon
        var ordinaryRoomId = Guid.NewGuid();
        var ordinaryRoomLocation = Builders.MakeLocation(
            _worldId,
            _stateId,
            roomId: ordinaryRoomId
        );
        var ordinaryRoom = Builders.MakeRoom(
            _dungeon.Id,
            id: ordinaryRoomId,
            worldId: _worldId,
            locationId: ordinaryRoomLocation.Id
        );
        _context.Locations.Add(ordinaryRoomLocation);
        _context.Rooms.Add(ordinaryRoom);
        await _context
            .Rooms.Where(r => r.Id == _bossRoom.Id)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await SeedLivingHostile(ordinaryRoomLocation.Id);

        // Act
        var result = await _handler.Handle(
            new SeedAssassinateQuestCommand
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
    public async Task Handle_ReturnsFalse_WhenTheSeedLocationHasNoCity()
    {
        // Arrange
        await SeedLivingHostile(_bossRoomLocation.Id);

        // Act
        var result = await _handler.Handle(
            new SeedAssassinateQuestCommand
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
    public async Task Handle_ReturnsFalse_WhenTheOnlyCandidateHasADisqualifyingProfession()
    {
        // Arrange — a baker isn't a plausible bounty giver
        await SeedLivingHostile(_bossRoomLocation.Id);
        await _context
            .Creatures.Where(creature => creature.Id == _giver.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.Profession, Profession.Baker),
                TestContext.Current.CancellationToken
            );

        // Act
        var result = await _handler.Handle(
            new SeedAssassinateQuestCommand
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
    public async Task Handle_ReturnsFalse_WhenNoBossChamberHasAnyLivingHostiles()
    {
        // Act — no hostiles seeded, so the boss chamber is empty
        var result = await _handler.Handle(
            new SeedAssassinateQuestCommand
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
    public async Task Handle_ReturnsFalse_WhenThePlayerAlreadyHasAnActiveKillQuestForTheOnlyTarget()
    {
        // Arrange
        var target = await SeedLivingHostile(_bossRoomLocation.Id);
        var playerId = Guid.NewGuid();
        var existingQuest = Builders.MakeQuest(_giver.Id, _worldId);
        var existingObjective = new KillCreatureObjective
        {
            WorldId = _worldId,
            QuestId = existingQuest.Id,
            CreatureId = target.Id,
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
            new SeedAssassinateQuestCommand
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
    }
}
