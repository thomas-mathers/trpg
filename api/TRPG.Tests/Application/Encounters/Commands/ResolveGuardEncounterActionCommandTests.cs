using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Exceptions;
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
        Guid encounterId
    ) =>
        new()
        {
            SessionId = _session.Id,
            WorldId = WorldId,
            PlayerId = _player.Id,
            Action = action,
            EncounterId = encounterId,
        };

    private async Task<JailFixture> SeedJail()
    {
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
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new JailFixture(cellsRoom.LocationId, exitDoor.Id, encounterLocation.Id);
    }

    private sealed record JailFixture(
        Guid CellsLocationId,
        Guid ExitDoorId,
        Guid EncounterLocationId
    );

    private async Task<GuardEncounter> SeedActiveEncounter(
        Guid locationId,
        Guid? triggeringCrimeId = null
    )
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
            TriggeringCrimeId = triggeringCrimeId,
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
            MakeCommand(new PayFineEncounterAction(), encounter.Id),
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
            MakeCommand(new PayFineEncounterAction(), encounter.Id),
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
            MakeCommand(new PayFineEncounterAction(), encounter.Id),
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
        var jail = await SeedJail();
        var encounter = await SeedActiveEncounter(jail.EncounterLocationId);

        // Act
        await _handler.Handle(
            MakeCommand(new GoToJailEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedPlayer = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(jail.CellsLocationId, updatedPlayer!.LocationId);

        var updatedDoor = await verifyContext.DoorConnectors.FindAsync(
            [jail.ExitDoorId],
            TestContext.Current.CancellationToken
        );
        Assert.True(updatedDoor!.IsLocked);
        Assert.Equal(
            _session.Playtime + GameClock.RealTimePerInGameHour * 24,
            updatedDoor.UnlocksAtPlaytime
        );
    }

    [Fact]
    public async Task Handle_GoToJail_ResolvesWitnessedCrimesAtTheLocationThePlayerIsTakenFrom()
    {
        // Arrange
        var jail = await SeedJail();
        _player.LocationId = jail.EncounterLocationId;
        var witness = Builders.MakeCreature(WorldId, locationId: jail.EncounterLocationId);
        var faction = Builders.MakeFaction(WorldId);
        var crime = new TheftCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = jail.EncounterLocationId,
            OwnerFactionId = faction.Id,
            OwnerCreatureId = witness.Id,
            OwnerName = witness.Name,
            Outcome = TheftCrimeOutcome.Taken,
            SourceOwnerId = Guid.NewGuid(),
            SourceOwnerType = OwnerType.Container,
        };
        _context.Creatures.Add(witness);
        _context.Factions.Add(faction);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(
            new CrimeWitness
            {
                WorldId = WorldId,
                CrimeId = crime.Id,
                CreatureId = witness.Id,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var encounter = await SeedActiveEncounter(jail.EncounterLocationId);

        // Act
        await _handler.Handle(
            MakeCommand(new GoToJailEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CrimeResolution.Reported, persistedCrime!.Resolution);

        var updatedPlayer = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(jail.CellsLocationId, updatedPlayer!.LocationId);

        var activeEncounters = await verifyContext.Encounters.CountAsync(
            e => e.PlayerId == _player.Id && e.State == EncounterState.Active,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(0, activeEncounters);
    }

    [Fact]
    public async Task Handle_GoToJail_SettlesTheTriggeringCrimeBeforeTheRelocationResolvesIt()
    {
        // Arrange — the jail move is what resolves the crime, so the settled outcome has to be
        // recorded first or the player pays the full unsettled penalty
        var jail = await SeedJail();
        _player.LocationId = jail.EncounterLocationId;
        var witness = Builders.MakeCreature(WorldId, locationId: jail.EncounterLocationId);
        var ownerFaction = Builders.MakeFaction(WorldId);
        var crime = new LockpickingCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = jail.EncounterLocationId,
            BuildingId = Guid.NewGuid(),
            BuildingName = "Locked Warehouse",
            OwnerFactionId = ownerFaction.Id,
        };
        _context.Creatures.Add(witness);
        _context.Factions.Add(ownerFaction);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(Builders.MakeCrimeWitness(crime.Id, witness.Id, WorldId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var encounter = await SeedActiveEncounter(
            jail.EncounterLocationId,
            triggeringCrimeId: crime.Id
        );

        // Act
        await _handler.Handle(
            MakeCommand(new GoToJailEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext
            .Crimes.OfType<LockpickingCrime>()
            .SingleAsync(c => c.Id == crime.Id, TestContext.Current.CancellationToken);
        Assert.Equal(LockpickingCrimeOutcome.SettledWithGuard, persistedCrime.Outcome);
        Assert.Equal(CrimeResolution.Reported, persistedCrime.Resolution);

        var reputation = await verifyContext.Reputations.SingleAsync(
            r => r.CreatureId == _player.Id && r.TargetId == ownerFaction.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(-4, reputation.Score);
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
            MakeCommand(new GoToJailEncounterAction(), encounter.Id),
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
            MakeCommand(new ResistArrestEncounterAction(), encounter.Id),
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
            MakeCommand(new ResistArrestEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(GuardEncounterResolutionOutcome.ResistedArrest, fact.Outcome);
        Assert.Null(fact.FineAmount);
        Assert.Null(fact.JailHours);
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFound_WhenTheEncounterDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(
                MakeCommand(new ResistArrestEncounterAction(), Guid.NewGuid()),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFound_WhenTheEncounterBelongsToAnotherWorld()
    {
        // Arrange
        var encounter = await SeedActiveEncounter(Guid.NewGuid());
        var command = new ResolveGuardEncounterActionCommand
        {
            SessionId = _session.Id,
            WorldId = Guid.NewGuid(),
            PlayerId = _player.Id,
            Action = new ResistArrestEncounterAction(),
            EncounterId = encounter.Id,
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFound_WhenTheEncounterBelongsToAnotherPlayer()
    {
        // Arrange
        var encounter = await SeedActiveEncounter(Guid.NewGuid());
        var command = new ResolveGuardEncounterActionCommand
        {
            SessionId = _session.Id,
            WorldId = WorldId,
            PlayerId = Guid.NewGuid(),
            Action = new ResistArrestEncounterAction(),
            EncounterId = encounter.Id,
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_ThrowsInvalidOperation_WhenTheEncounterIsAlreadyCompleted()
    {
        // Arrange
        var encounter = await SeedActiveEncounter(Guid.NewGuid());
        await _handler.Handle(
            MakeCommand(new ResistArrestEncounterAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                MakeCommand(new ResistArrestEncounterAction(), encounter.Id),
                TestContext.Current.CancellationToken
            )
        );
    }
}
