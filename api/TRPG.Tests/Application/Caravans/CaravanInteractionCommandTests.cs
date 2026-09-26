using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Caravans.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Caravans;

public sealed class CaravanInteractionCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RouteTraveler _caravan = null!;
    private Creature _player = null!;
    private Creature _member = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();

        var locationA = Guid.NewGuid();
        var locationB = Guid.NewGuid();
        var route = Builders.MakeCaravanRoute(_worldId);
        var connectorA = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 10,
            worldId: _worldId
        );
        var connectorB = Builders.MakeTravelConnector(
            Guid.NewGuid(),
            distance: 10,
            worldId: _worldId
        );
        _caravan = Builders.MakeCaravan(route.Id, _worldId);
        _player = Builders.MakeCreature(_worldId, locationId: locationA);
        _member = Builders.MakeCreature(_worldId, locationId: locationA);
        _context.Routes.Add(route);
        _context.RouteSteps.AddRange(
            Builders.MakeCaravanRouteStop(route.Id, 0, locationA, connectorA.ConnectorId),
            Builders.MakeCaravanRouteStop(route.Id, 1, locationB, connectorB.ConnectorId)
        );
        _context.TravelConnectors.AddRange(connectorA, connectorB);
        _context.RouteTravelers.Add(_caravan);
        _context.Creatures.AddRange(_player, _member);
        _context.RouteTravelerMembers.Add(
            Builders.MakeRouteTravelerMember(_caravan.Id, _member.Id, _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Commands_PauseAndResumeTheCaravan_WhenInteractionBeginsAndEnds()
    {
        var begin = _serviceProvider.GetRequiredService<
            ICommandHandler<BeginCaravanInteractionCommand>
        >();
        var end = _serviceProvider.GetRequiredService<
            ICommandHandler<EndCaravanInteractionCommand>
        >();

        await begin.Handle(
            new BeginCaravanInteractionCommand
            {
                WorldId = _worldId,
                PlayerId = _player.Id,
                CaravanId = _caravan.Id,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        var pausedCaravan = await _context.RouteTravelers.SingleAsync(
            traveler => traveler.Id == _caravan.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(GameClock.Epoch, pausedCaravan.PausedAtGameTime);
        var participantIds = new[] { _player.Id, _member.Id };
        Assert.True(
            await _context
                .Creatures.Where(creature => participantIds.Contains(creature.Id))
                .AllAsync(creature => creature.IsEngaged, TestContext.Current.CancellationToken)
        );

        await end.Handle(
            new EndCaravanInteractionCommand
            {
                WorldId = _worldId,
                PlayerId = _player.Id,
                CaravanId = _caravan.Id,
                GameTime = GameClock.Epoch + TimeSpan.FromMinutes(30),
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        var resumedCaravan = await _context.RouteTravelers.SingleAsync(
            traveler => traveler.Id == _caravan.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Null(resumedCaravan.PausedAtGameTime);
        Assert.Equal(
            _caravan.StartedAtGameTime + TimeSpan.FromMinutes(30),
            resumedCaravan.StartedAtGameTime
        );
        Assert.False(
            await _context.Creatures.AnyAsync(
                creature => participantIds.Contains(creature.Id) && creature.IsEngaged,
                TestContext.Current.CancellationToken
            )
        );
    }
}
