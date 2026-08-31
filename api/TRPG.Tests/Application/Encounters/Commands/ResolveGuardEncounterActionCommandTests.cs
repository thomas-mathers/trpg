using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Inventory;
using TRPG.Application.WorldGeneration;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class ResolveGuardEncounterActionCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveGuardEncounterActionCommandHandler _handler = null!;
    private readonly Faction _cityFaction = Builders.MakeFaction(WorldId, isCityFaction: true);
    private readonly GameSession _session = Builders.MakeGameSession(
        WorldId,
        Guid.NewGuid(),
        playtime: TimeSpan.FromHours(10)
    );
    private Creature _player = null!;
    private Creature _guard = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .Configure<GuardEncounterOptions>(new ConfigurationBuilder().Build())
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResolveGuardEncounterActionCommandHandler>();

        _player = Builders.MakeCreature(WorldId);
        _guard = Builders.MakeCreature(WorldId, profession: Profession.Guard);
        _context.Creatures.AddRange(_player, _guard);
        _context.Factions.Add(_cityFaction);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private ResolveGuardEncounterActionCommand MakeCommand(
        GuardEncounterAction action,
        Guid encounterId,
        Guid encounterLocationId
    ) =>
        new()
        {
            SessionId = _session.Id,
            Playtime = _session.Playtime,
            WorldId = WorldId,
            PlayerId = _player.Id,
            Action = action,
            EncounterId = encounterId,
            GuardCreatureId = _guard.Id,
            CityFactionId = _cityFaction.Id,
            GuardName = _guard.Name,
            EncounterLocationId = encounterLocationId,
            LocationName = "Market Square",
            ReputationScore = -50,
            FineAmount = 250,
            JailHours = 24,
        };

    private async Task<GuardEncounter> SeedActiveEncounter(Guid locationId)
    {
        var encounter = new GuardEncounter
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = locationId,
            GuardCreatureId = _guard.Id,
            CityFactionId = _cityFaction.Id,
            GuardName = _guard.Name,
            LocationName = "Market Square",
            ReputationScore = -50,
            FineAmount = 250,
            JailHours = 24,
        };
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return encounter;
    }

    [Fact]
    public async Task Handle_PayFine_RemovesGoldEqualToTheFineAmount()
    {
        // Arrange
        var encounter = await SeedActiveEncounter(Guid.NewGuid());
        var gold = new Gold
        {
            WorldId = WorldId,
            Name = "Gold",
            Quantity = 500,
            Ownership = new ItemOwnership { OwnerId = _player.Id, OwnerType = OwnerType.Creature },
        };
        _context.Items.Add(gold);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            MakeCommand(new PayFineEncounterAction(), encounter.Id, encounter.LocationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedGold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                g => g.Ownership.OwnerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(250, updatedGold.Quantity);
    }

    [Fact]
    public async Task Handle_PayFine_ZeroesOutReputationWithTheCityFaction()
    {
        // Arrange
        var encounter = await SeedActiveEncounter(Guid.NewGuid());
        _context.Items.Add(
            new Gold
            {
                WorldId = WorldId,
                Name = "Gold",
                Quantity = 500,
                Ownership = new ItemOwnership
                {
                    OwnerId = _player.Id,
                    OwnerType = OwnerType.Creature,
                },
            }
        );
        _context.Reputations.Add(
            new Reputation
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                TargetId = _cityFaction.Id,
                TargetType = ReputationTargetType.Faction,
                Score = -50,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            MakeCommand(new PayFineEncounterAction(), encounter.Id, encounter.LocationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var reputation = await verifyContext.Reputations.SingleAsync(
            r => r.CreatureId == _player.Id && r.TargetId == _cityFaction.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(0, reputation.Score);
    }

    [Fact]
    public async Task Handle_PayFine_CompletesTheEncounter()
    {
        // Arrange
        var encounter = await SeedActiveEncounter(Guid.NewGuid());
        _context.Items.Add(
            new Gold
            {
                WorldId = WorldId,
                Name = "Gold",
                Quantity = 500,
                Ownership = new ItemOwnership
                {
                    OwnerId = _player.Id,
                    OwnerType = OwnerType.Creature,
                },
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            MakeCommand(new PayFineEncounterAction(), encounter.Id, encounter.LocationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext.Encounters.SingleAsync(
            e => e.Id == encounter.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(EncounterState.Completed, persisted.State);
    }

    [Fact]
    public async Task Handle_GoToJail_MovesThePlayerToTheCellsAndLocksTheExitUntilTheSentenceEnds()
    {
        // Arrange
        var cityId = Guid.NewGuid();
        var jailLocation = Builders.MakeLocation(cityId: cityId);
        var jail = Builders.MakeBuilding(
            exteriorLocationId: jailLocation.Id,
            buildingType: BuildingType.Jail
        );
        var guardStationRoom = Builders.MakeRoom(jail.Id, name: "Guard Station");
        var cellsRoom = Builders.MakeRoom(jail.Id, name: JailRoomNames.Cells);
        var exitConnector = Builders.MakeLocationConnector(
            cellsRoom.LocationId,
            destinationLocationId: guardStationRoom.LocationId
        );
        var exitDoor = Builders.MakeDoorConnector(exitConnector.Id);
        var encounterLocation = Builders.MakeLocation(cityId: cityId);
        _context.Locations.AddRange(jailLocation, encounterLocation);
        _context.Buildings.Add(jail);
        _context.Rooms.AddRange(guardStationRoom, cellsRoom);
        _context.LocationConnectors.Add(exitConnector);
        _context.DoorConnectors.Add(exitDoor);
        var encounter = await SeedActiveEncounter(encounterLocation.Id);

        // Act
        await _handler.Handle(
            MakeCommand(new GoToJailEncounterAction(), encounter.Id, encounter.LocationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedPlayer = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(cellsRoom.LocationId, updatedPlayer!.LocationId);

        var updatedDoor = await verifyContext.DoorConnectors.FindAsync(
            [exitDoor.Id],
            TestContext.Current.CancellationToken
        );
        Assert.True(updatedDoor!.IsLocked);
        Assert.Equal(
            _session.Playtime + GameClock.RealTimePerInGameHour * 24,
            updatedDoor.UnlocksAtPlaytime
        );
    }

    [Fact]
    public async Task Handle_GoToJail_RestoresEnoughReputationToClearTheEncounterThreshold()
    {
        // Arrange
        var cityId = Guid.NewGuid();
        var jailLocation = Builders.MakeLocation(cityId: cityId);
        var jail = Builders.MakeBuilding(
            exteriorLocationId: jailLocation.Id,
            buildingType: BuildingType.Jail
        );
        var guardStationRoom = Builders.MakeRoom(jail.Id, name: "Guard Station");
        var cellsRoom = Builders.MakeRoom(jail.Id, name: JailRoomNames.Cells);
        var exitConnector = Builders.MakeLocationConnector(
            cellsRoom.LocationId,
            destinationLocationId: guardStationRoom.LocationId
        );
        var exitDoor = Builders.MakeDoorConnector(exitConnector.Id);
        var encounterLocation = Builders.MakeLocation(cityId: cityId);
        _context.Locations.AddRange(jailLocation, encounterLocation);
        _context.Buildings.Add(jail);
        _context.Rooms.AddRange(guardStationRoom, cellsRoom);
        _context.LocationConnectors.Add(exitConnector);
        _context.DoorConnectors.Add(exitDoor);
        _context.Reputations.Add(
            new Reputation
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                TargetId = _cityFaction.Id,
                TargetType = ReputationTargetType.Faction,
                Score = -50,
            }
        );
        var encounter = await SeedActiveEncounter(encounterLocation.Id);

        // Act
        await _handler.Handle(
            MakeCommand(new GoToJailEncounterAction(), encounter.Id, encounter.LocationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var reputation = await verifyContext.Reputations.SingleAsync(
            r => r.CreatureId == _player.Id && r.TargetId == _cityFaction.Id,
            TestContext.Current.CancellationToken
        );
        // MakeCommand uses ReputationScore = -50; default ReputationThreshold is -25, so the
        // restored score must land strictly above -25 to avoid immediately re-triggering.
        Assert.Equal(-24, reputation.Score);
    }

    [Fact]
    public async Task Handle_ResistArrest_AlertsTheGuardAndStartsAFight()
    {
        // Arrange
        var encounter = await SeedActiveEncounter(Guid.NewGuid());

        // Act
        await _handler.Handle(
            MakeCommand(new ResistArrestEncounterAction(), encounter.Id, encounter.LocationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedGuard = await verifyContext.Creatures.FindAsync(
            [_guard.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Alerted, updatedGuard!.State);

        var fight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(f => f.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Contains(_guard.Id, fight.CombatantIds);
    }

    [Fact]
    public async Task Handle_ResistArrest_ReturnsAFactWithNoFineOrJailAmount()
    {
        // Arrange
        var encounter = await SeedActiveEncounter(Guid.NewGuid());

        // Act
        var fact = await _handler.Handle(
            MakeCommand(new ResistArrestEncounterAction(), encounter.Id, encounter.LocationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(GuardEncounterResolutionOutcome.ResistedArrest, fact.Outcome);
        Assert.Null(fact.FineAmount);
        Assert.Null(fact.JailHours);
    }
}
