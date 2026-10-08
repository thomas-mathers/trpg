using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class CreatureEngagementCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<EngageCreaturesCommand> _engage = null!;
    private ICommandHandler<ReleaseCreaturesCommand> _release = null!;
    private Creature _first = null!;
    private Creature _second = null!;
    private RouteTraveler _traveler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _engage = _serviceProvider.GetRequiredService<ICommandHandler<EngageCreaturesCommand>>();
        _release = _serviceProvider.GetRequiredService<ICommandHandler<ReleaseCreaturesCommand>>();

        var route = Builders.MakeCaravanRoute(_worldId);
        _traveler = Builders.MakeCaravan(route.Id, _worldId, phaseOffsetHours: 0);
        _first = Builders.MakeCreature(_worldId);
        _second = Builders.MakeCreature(_worldId);
        _context.Routes.Add(route);
        _context.RouteTravelers.Add(_traveler);
        _context.Creatures.AddRange(_first, _second);
        _context.RouteTravelerMembers.AddRange(
            Builders.MakeRouteTravelerMember(_traveler.Id, _first.Id, _worldId),
            Builders.MakeRouteTravelerMember(_traveler.Id, _second.Id, _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private Task Engage(Guid creatureId, GameInstant gameTime) =>
        _engage.Handle(
            new EngageCreaturesCommand
            {
                WorldId = _worldId,
                CreatureIds = [creatureId],
                GameTime = gameTime,
            },
            TestContext.Current.CancellationToken
        );

    private Task Release(Guid creatureId, GameInstant gameTime) =>
        _release.Handle(
            new ReleaseCreaturesCommand
            {
                WorldId = _worldId,
                CreatureIds = [creatureId],
                GameTime = gameTime,
            },
            TestContext.Current.CancellationToken
        );

    [Fact]
    public async Task Handle_PausesAndResumesWholeRouteGroup_WhenMembersEngageAndRelease()
    {
        var pausedAt = GameClock.Epoch + TimeSpan.FromHours(2);
        var releasedAt = GameClock.Epoch + TimeSpan.FromHours(5);

        await _engage.Handle(
            new EngageCreaturesCommand
            {
                WorldId = _worldId,
                CreatureIds = [_first.Id],
                GameTime = pausedAt,
            },
            TestContext.Current.CancellationToken
        );
        await _engage.Handle(
            new EngageCreaturesCommand
            {
                WorldId = _worldId,
                CreatureIds = [_second.Id],
                GameTime = pausedAt + TimeSpan.FromHours(1),
            },
            TestContext.Current.CancellationToken
        );
        await _release.Handle(
            new ReleaseCreaturesCommand
            {
                WorldId = _worldId,
                CreatureIds = [_first.Id],
                GameTime = releasedAt,
            },
            TestContext.Current.CancellationToken
        );

        await using (var pausedContext = db.CreateContext())
        {
            var paused = await pausedContext.RouteTravelers.SingleAsync(
                traveler => traveler.Id == _traveler.Id,
                TestContext.Current.CancellationToken
            );
            Assert.Equal(pausedAt, paused.PausedAtGameTime);
            Assert.Equal(_traveler.StartedAtGameTime, paused.StartedAtGameTime);
        }

        await _release.Handle(
            new ReleaseCreaturesCommand
            {
                WorldId = _worldId,
                CreatureIds = [_second.Id],
                GameTime = releasedAt,
            },
            TestContext.Current.CancellationToken
        );

        await using var verifyContext = db.CreateContext();
        var creatures = await verifyContext
            .Creatures.Where(creature => creature.Id == _first.Id || creature.Id == _second.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var resumed = await verifyContext.RouteTravelers.SingleAsync(
            traveler => traveler.Id == _traveler.Id,
            TestContext.Current.CancellationToken
        );
        Assert.All(creatures, creature => Assert.False(creature.IsEngaged));
        Assert.Null(resumed.PausedAtGameTime);
        Assert.Equal(
            _traveler.StartedAtGameTime + (releasedAt - pausedAt),
            resumed.StartedAtGameTime
        );
    }

    [Fact]
    public async Task Handle_ShiftsAnEntryWalkByThePause_WhenAWalkingCreatureIsEngagedAndReleased()
    {
        var walkStartedAt = GameClock.Epoch + TimeSpan.FromHours(1);
        var walker = Builders.MakeCreature(
            _worldId,
            entry: new Point(0, 0),
            enteredAt: walkStartedAt
        );
        _context.Creatures.Add(walker);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
        await Engage(walker.Id, walkStartedAt + TimeSpan.FromSeconds(5));

        await Release(walker.Id, walkStartedAt + TimeSpan.FromSeconds(65));

        await using var verifyContext = db.CreateContext();
        var stored = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == walker.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(walkStartedAt + TimeSpan.FromSeconds(60), stored.EnteredAt);
    }

    [Fact]
    public async Task Handle_ShiftsAnExitWalkByThePause_WhenAWalkingCreatureIsEngagedAndReleased()
    {
        var walkStartedAt = GameClock.Epoch + TimeSpan.FromHours(1);
        var walker = Builders.MakeCreature(
            _worldId,
            exit: new Point(10, 0),
            departedAt: walkStartedAt
        );
        _context.Creatures.Add(walker);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
        await Engage(walker.Id, walkStartedAt + TimeSpan.FromSeconds(5));

        await Release(walker.Id, walkStartedAt + TimeSpan.FromSeconds(65));

        await using var verifyContext = db.CreateContext();
        var stored = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == walker.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(walkStartedAt + TimeSpan.FromSeconds(60), stored.DepartedAt);
    }

    [Fact]
    public async Task Handle_ClearsTheWalkPause_WhenAWalkingCreatureIsReleased()
    {
        var walkStartedAt = GameClock.Epoch + TimeSpan.FromHours(1);
        var walker = Builders.MakeCreature(
            _worldId,
            entry: new Point(0, 0),
            enteredAt: walkStartedAt
        );
        _context.Creatures.Add(walker);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
        await Engage(walker.Id, walkStartedAt + TimeSpan.FromSeconds(5));

        await Release(walker.Id, walkStartedAt + TimeSpan.FromSeconds(65));

        await using var verifyContext = db.CreateContext();
        var stored = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == walker.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Null(stored.WalkPausedAt);
    }

    [Fact]
    public async Task Handle_RejectsEngagement_WhenCreatureIsAlreadyEngaged()
    {
        var command = new EngageCreaturesCommand
        {
            WorldId = _worldId,
            CreatureIds = [_first.Id],
            GameTime = GameClock.Epoch,
        };
        await _engage.Handle(command, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engage.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Equal("A creature is already engaged.", exception.Message);
    }

    [Fact]
    public async Task CreatureInteraction_EngagesAndReleasesBothCreatures_WhenTheyShareALocation()
    {
        var locationId = Guid.NewGuid();
        var player = Builders.MakeCreature(_worldId, locationId: locationId);
        var npc = Builders.MakeCreature(_worldId, locationId: locationId);
        _context.Creatures.AddRange(player, npc);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var begin = _serviceProvider.GetRequiredService<
            ICommandHandler<BeginCreatureInteractionCommand>
        >();
        var end = _serviceProvider.GetRequiredService<
            ICommandHandler<EndCreatureInteractionCommand>
        >();
        var startedAt = GameClock.Epoch + TimeSpan.FromHours(2);
        await begin.Handle(
            new BeginCreatureInteractionCommand
            {
                WorldId = _worldId,
                PlayerId = player.Id,
                CreatureId = npc.Id,
                GameTime = startedAt,
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        var engaged = await _context
            .Creatures.Where(creature => creature.Id == player.Id || creature.Id == npc.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.All(engaged, creature => Assert.True(creature.IsEngaged));

        await end.Handle(
            new EndCreatureInteractionCommand
            {
                WorldId = _worldId,
                PlayerId = player.Id,
                CreatureId = npc.Id,
                GameTime = startedAt + TimeSpan.FromHours(1),
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        var released = await _context
            .Creatures.Where(creature => creature.Id == player.Id || creature.Id == npc.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.All(released, creature => Assert.False(creature.IsEngaged));
    }
}
