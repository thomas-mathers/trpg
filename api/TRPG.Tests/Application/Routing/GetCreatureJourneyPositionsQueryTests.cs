using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Routing;

public sealed class GetCreatureJourneyPositionsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant Now = new(new DateTime(1000, 1, 1, 12, 0, 0));

    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_TrimsWaypointsPassedBeforeTheProjectedPosition()
    {
        // Arrange
        var world = Builders.MakeWorld();
        var location = Builders.MakeLocation(worldId: world.Id);
        var creature = Builders.MakeCreature(world.Id, locationId: location.Id, movementSpeed: 50);
        var origin = Builders.MakeTravelNode(location.Id, worldId: world.Id);
        var destination = Builders.MakeTravelNode(location.Id, x: 30, worldId: world.Id);
        var connector = Builders.MakePointConnector(
            location.Id,
            origin.Id,
            destination.Id,
            distance: 30,
            worldId: world.Id
        );
        var journey = new Journey
        {
            WorldId = world.Id,
            Status = JourneyStatus.Traveling,
            CheckpointLegIndex = 0,
            CheckpointLegProgressMeters = 25,
            CheckpointedAt = Now,
        };
        var leg = new JourneyLeg
        {
            JourneyId = journey.Id,
            Index = 0,
            FromNodeId = origin.Id,
            ToNodeId = destination.Id,
            ConnectorId = connector.Id,
            Distance = 30,
            Path = new Polyline
            {
                Points = [new Point(0, 0), new Point(10, 0), new Point(20, 0), new Point(30, 0)],
            },
        };
        _context.Worlds.Add(world);
        _context.Locations.Add(location);
        _context.Creatures.Add(creature);
        _context.TravelNodes.AddRange(origin, destination);
        _context.PointConnectors.Add(connector);
        _context.Journeys.Add(journey);
        _context.JourneyLegs.Add(leg);
        _context.JourneyMembers.Add(
            new JourneyMember { JourneyId = journey.Id, CreatureId = creature.Id }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = _services.GetRequiredService<
            IQueryHandler<
                GetCreatureJourneyPositionsQuery,
                IReadOnlyDictionary<Guid, CreatureJourneyPosition>
            >
        >();

        // Act
        var positions = await handler.Handle(
            new GetCreatureJourneyPositionsQuery
            {
                LocationId = location.Id,
                GameTime = Now,
                CreatureIds = [creature.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([new Point(25, 0), new Point(30, 0)], positions[creature.Id].Path);
    }
}
