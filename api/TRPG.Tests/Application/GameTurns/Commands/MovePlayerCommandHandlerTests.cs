using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns.Commands;

[Collection("Database")]
public sealed class MovePlayerCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private MovePlayerCommandHandler _handler = null!;
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<MovePlayerCommandHandler>();

        _session = Builders.MakeGameSession(WorldId, Guid.NewGuid());
        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId);
        _context.GameSessions.Add(_session);
        _context.States.Add(state);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ResolvesWitnessedTheft_WhenThePlayerLeavesTheCrimeScene()
    {
        // Arrange
        var oldLocation = Builders.MakeLocation(WorldId, _stateId);
        var newLocation = Builders.MakeLocation(WorldId, _stateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id);
        var witness = Builders.MakeCreature(WorldId, locationId: oldLocation.Id);
        var faction = Builders.MakeFaction(WorldId);
        var crime = new TheftCrime
        {
            WorldId = WorldId,
            PlayerId = player.Id,
            LocationId = oldLocation.Id,
            OwnerFactionId = faction.Id,
            OwnerCreatureId = witness.Id,
            OwnerName = witness.Name,
            Outcome = TheftCrimeOutcome.Taken,
            SourceOwnerId = Guid.NewGuid(),
            SourceOwnerType = OwnerType.Container,
        };
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.Creatures.AddRange(player, witness);
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

        // Act
        await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationLocationId = newLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        var persistedWitness = await verifyContext.CrimeWitnesses.SingleAsync(
            candidate => candidate.CrimeId == crime.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(CrimeResolution.Reported, persistedCrime!.Resolution);
        Assert.Equal(CrimeWitnessResolution.Reported, persistedWitness.Resolution);
    }

    [Fact]
    public async Task Handle_CreatesAnActiveEncounter_WhenMovingIntoALocationWithAnEngagingGroup()
    {
        // Arrange
        var oldLocation = Builders.MakeLocation(WorldId, _stateId);
        var newLocation = Builders.MakeLocation(WorldId, _stateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id, level: 1);
        var faction = Builders.MakeFaction(WorldId, aggression: 150);
        var monster = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: newLocation.Id,
            level: 1
        );
        var group = Builders.MakeEncounterGroup(WorldId, newLocation.Id, faction.Id);
        var member = Builders.MakeEncounterGroupMember(WorldId, group.Id, monster.Id);
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.Creatures.AddRange(player, monster);
        _context.Factions.Add(faction);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.Add(member);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationLocationId = newLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result.Encounter);
        Assert.Equal(faction.Name, result.Encounter.FactionName);

        await using var verifyContext = db.CreateContext();
        var encounter = await verifyContext
            .Encounters.OfType<HostileEncounter>()
            .SingleAsync(e => e.PlayerId == player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EncounterState.Active, encounter.State);

        var movedPlayer = await verifyContext.Creatures.SingleAsync(
            c => c.Id == player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(oldLocation.Id, movedPlayer.PreviousLocationId);
    }

    [Fact]
    public async Task Handle_CreatesAnActiveGuardEncounter_WhenMovingToALowRepGuardsLocation()
    {
        // Arrange
        var oldLocation = Builders.MakeLocation(WorldId, _stateId);
        var newLocation = Builders.MakeLocation(WorldId, _stateId);
        var player = Builders.MakeCreature(WorldId, locationId: oldLocation.Id);
        var cityFaction = Builders.MakeFaction(WorldId, isCityFaction: true);
        var guard = Builders.MakeCreature(
            WorldId,
            profession: Profession.Guard,
            locationId: newLocation.Id
        );
        _context.Locations.AddRange(oldLocation, newLocation);
        _context.Creatures.AddRange(player, guard);
        _context.Factions.Add(cityFaction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, cityFaction.Id, guard.Id));
        _context.Reputations.Add(
            new Reputation
            {
                WorldId = WorldId,
                CreatureId = player.Id,
                TargetId = cityFaction.Id,
                TargetType = ReputationTargetType.Faction,
                Score = -50,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["EncounterChance"] = 1f.ToString(CultureInfo.InvariantCulture),
                }
            )
            .Build();
        await using var guardServiceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .Configure<GuardEncounterOptions>(configuration)
            .BuildServiceProvider();
        var guardHandler = guardServiceProvider.GetRequiredService<MovePlayerCommandHandler>();

        // Act
        var result = await guardHandler.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                SessionId = _session.Id,
                DestinationLocationId = newLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result.GuardEncounter);
        Assert.Equal(guard.Id, result.GuardEncounter.GuardCreatureId);

        await using var verifyContext = db.CreateContext();
        var encounter = await verifyContext
            .Encounters.OfType<GuardEncounter>()
            .SingleAsync(e => e.PlayerId == player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EncounterState.Active, encounter.State);
    }
}
