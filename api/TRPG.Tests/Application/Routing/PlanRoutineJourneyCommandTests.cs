using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Routing.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Routing;

public sealed class PlanRoutineJourneyCommandTests : IAsyncLifetime, IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture db;
    private static readonly GameInstant Now = new(new DateTime(1000, 1, 1, 7, 0, 0));

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<PlanRoutineJourneyCommand, Guid?> _handler = null!;
    private readonly World _world = Builders.MakeWorld();
    private readonly Guid _homeLocationId = Guid.NewGuid();
    private readonly Guid _workLocationId = Guid.NewGuid();
    private readonly Creature _worker;
    private readonly TravelNode _homeNode;
    private readonly TravelNode _exitNode;
    private readonly TravelNode _arrivalNode;

    public PlanRoutineJourneyCommandTests(DatabaseFixture db)
    {
        this.db = db;
        _homeNode = Builders.MakeTravelNode(_homeLocationId, worldId: _world.Id);
        _exitNode = Builders.MakeTravelNode(_homeLocationId, x: 2, worldId: _world.Id);
        _arrivalNode = Builders.MakeTravelNode(_workLocationId, worldId: _world.Id);
        _worker = Builders.MakeCreature(_world.Id, locationId: _homeLocationId, movementSpeed: 50);
        _worker.CurrentTravelNodeId = _homeNode.Id;
    }

    public async ValueTask InitializeAsync()
    {
        var door = Builders.MakeLocationConnector(_homeLocationId, _workLocationId, _world.Id);
        door.OriginNodeId = _exitNode.Id;
        door.DestinationNodeId = _arrivalNode.Id;
        var homeWalk = Builders.MakePointConnector(
            _homeLocationId,
            _homeNode.Id,
            _exitNode.Id,
            2,
            _world.Id
        );
        var job = Builders.MakeCreatureJob(
            _worker.Id,
            action: CreatureJobAction.Work,
            startHour: 8,
            endHour: 16,
            locationId: _workLocationId,
            worldId: _world.Id
        );
        _context = db.CreateContext();
        _context.Worlds.Add(_world);
        _context.Creatures.Add(_worker);
        _context.TravelNodes.AddRange(_homeNode, _exitNode, _arrivalNode);
        _context.LocationConnectors.Add(door);
        _context.PointConnectors.Add(homeWalk);
        _context.Props.Add(
            Builders.MakeWorkstation(
                _world.Id,
                _workLocationId,
                workstationType: WorkstationType.Trade
            )
        );
        _context.CreatureJobs.Add(job);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<PlanRoutineJourneyCommand, Guid?>
        >();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RoutesWorkOnlyToTheDestinationLocationNetwork()
    {
        // Arrange
        var command = new PlanRoutineJourneyCommand
        {
            CreatureId = _worker.Id,
            PlannedAt = Now,
            TimeScale = 1,
            ArrivalStagger = TimeSpan.Zero,
        };

        // Act
        var journeyId = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var journey = await _context.Journeys.SingleAsync(
            journey => journey.Id == journeyId,
            TestContext.Current.CancellationToken
        );
        var legs = await _context
            .JourneyLegs.Where(leg => leg.JourneyId == journey.Id)
            .OrderBy(leg => leg.Index)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(JourneyStatus.Planned, journey.Status);
        Assert.Equal(_arrivalNode.Id, legs[^1].ToNodeId);
        Assert.Equal(2, legs.Length);
        Assert.Equal(_homeNode.Id, legs[0].FromNodeId);
    }
}
