using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Routing;

public sealed class JourneyPersistenceTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task SaveChanges_RejectsConcurrentActiveJourneyMembership()
    {
        var (worldId, creatureId) = await SeedCreatureAsync();
        var firstJourney = new Journey
        {
            WorldId = worldId,
            Status = JourneyStatus.Planned,
            PlannedAt = GameClock.Epoch,
            DepartureAt = GameClock.Epoch,
            CheckpointedAt = GameClock.Epoch,
        };
        _context.Journeys.Add(firstJourney);
        _context.JourneyMembers.Add(
            new JourneyMember { JourneyId = firstJourney.Id, CreatureId = creatureId }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var secondJourney = new Journey
        {
            WorldId = worldId,
            Status = JourneyStatus.Traveling,
            PlannedAt = GameClock.Epoch,
            DepartureAt = GameClock.Epoch,
            CheckpointedAt = GameClock.Epoch,
        };
        _context.Journeys.Add(secondJourney);
        _context.JourneyMembers.Add(
            new JourneyMember { JourneyId = secondJourney.Id, CreatureId = creatureId }
        );

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _context.SaveChangesAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SaveChanges_RejectsDuplicateLegIndicesWithinACircuit()
    {
        var world = Builders.MakeWorld();
        var circuit = new TravelCircuit { WorldId = world.Id, Name = "Test circuit" };
        var fromNode = Builders.MakeTravelNode(Guid.NewGuid(), worldId: world.Id);
        var toNode = Builders.MakeTravelNode(Guid.NewGuid(), worldId: world.Id);
        var connector = Builders.MakePointConnector(
            fromNode.LocationId,
            fromNode.Id,
            toNode.Id,
            1,
            world.Id
        );
        _context.Worlds.Add(world);
        _context.TravelCircuits.Add(circuit);
        _context.TravelNodes.AddRange(fromNode, toNode);
        _context.PointConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _context.TravelCircuitLegs.AddRange(
            new TravelCircuitLeg
            {
                TravelCircuitId = circuit.Id,
                Index = 0,
                FromNodeId = fromNode.Id,
                ToNodeId = toNode.Id,
                ConnectorId = connector.Id,
            },
            new TravelCircuitLeg
            {
                TravelCircuitId = circuit.Id,
                Index = 0,
                FromNodeId = fromNode.Id,
                ToNodeId = toNode.Id,
                ConnectorId = connector.Id,
            }
        );

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _context.SaveChangesAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SaveChanges_RejectsAnOpenCircuit()
    {
        var world = Builders.MakeWorld();
        var circuit = new TravelCircuit { WorldId = world.Id, Name = "Test circuit" };
        var fromNode = Builders.MakeTravelNode(Guid.NewGuid(), worldId: world.Id);
        var toNode = Builders.MakeTravelNode(Guid.NewGuid(), worldId: world.Id);
        var connector = Builders.MakePointConnector(
            fromNode.LocationId,
            fromNode.Id,
            toNode.Id,
            1,
            world.Id
        );
        _context.Worlds.Add(world);
        _context.TravelCircuits.Add(circuit);
        _context.TravelNodes.AddRange(fromNode, toNode);
        _context.PointConnectors.Add(connector);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _context.TravelCircuitLegs.Add(
            new TravelCircuitLeg
            {
                TravelCircuitId = circuit.Id,
                Index = 0,
                FromNodeId = fromNode.Id,
                ToNodeId = toNode.Id,
                ConnectorId = connector.Id,
            }
        );

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _context.SaveChangesAsync(TestContext.Current.CancellationToken)
        );
    }

    private async Task<(Guid WorldId, Guid CreatureId)> SeedCreatureAsync()
    {
        var world = Builders.MakeWorld();
        var creature = Builders.MakeCreature(world.Id);
        _context.Worlds.Add(world);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (world.Id, creature.Id);
    }
}
