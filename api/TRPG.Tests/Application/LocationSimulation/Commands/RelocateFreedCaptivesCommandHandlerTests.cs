using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class RelocateFreedCaptivesCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid GiverId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RelocateFreedCaptivesCommandHandler _handler = null!;
    private Location _cellLocation = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<RelocateFreedCaptivesCommandHandler>();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId);
        _cellLocation = Builders.MakeLocation(WorldId, _stateId);
        _player = Builders.MakeCreature(WorldId, locationId: _cellLocation.Id);
        _context.States.Add(state);
        _context.Locations.Add(_cellLocation);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Guid> SeedFreedCaptive()
    {
        var captive = Builders.MakeCreature(WorldId, locationId: _cellLocation.Id);
        captive.MovementSpeed = 5;
        var quest = Builders.MakeQuest(GiverId, WorldId);
        var objective = new FreeCreatureObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            CreatureId = captive.Id,
        };
        _context.Creatures.Add(captive);
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return captive.Id;
    }

    [Fact]
    public async Task Handle_StartsAFreedCaptiveWalking_ToTheirDueJobLocation()
    {
        // Arrange
        var captiveId = await SeedFreedCaptive();
        var homeLocation = Builders.MakeLocation(WorldId, _stateId);
        var via = Builders.MakeLocation(WorldId, _stateId);
        var topology = new WalkableTopology(WorldId, walkMetersPerLocation: 10_000);
        topology.ConnectBothWays(_cellLocation.Id, via.Id);
        topology.ConnectBothWays(via.Id, homeLocation.Id);
        _context.Locations.AddRange(homeLocation, via);
        topology.AddTo(_context);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                captiveId,
                action: CreatureJobAction.Work,
                locationId: homeLocation.Id,
                worldId: WorldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new RelocateFreedCaptivesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = _cellLocation.Id,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var relocated = await verifyContext.Creatures.FindAsync(
            [captiveId],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(via.Id, relocated!.LocationId);
        Assert.Equal(CreatureMovement.Walking, relocated.Movement);
        Assert.Contains(
            await verifyContext.RouteTravelerMembers.ToArrayAsync(
                TestContext.Current.CancellationToken
            ),
            member => member.CreatureId == captiveId
        );
    }

    [Fact]
    public async Task Handle_DeletesAFreedCaptive_WhenTheyHaveNoCreatureJobRows()
    {
        // Arrange
        var captiveId = await SeedFreedCaptive();

        // Act
        await _handler.Handle(
            new RelocateFreedCaptivesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = _cellLocation.Id,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.Null(
            await verifyContext.Creatures.FindAsync(
                [captiveId],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_IgnoresACaptive_WhoIsStillRestrained()
    {
        // Arrange
        var captive = Builders.MakeCreature(
            WorldId,
            locationId: _cellLocation.Id,
            isRestrained: true
        );
        var quest = Builders.MakeQuest(GiverId, WorldId);
        var objective = new FreeCreatureObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            CreatureId = captive.Id,
        };
        _context.Creatures.Add(captive);
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new RelocateFreedCaptivesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = _cellLocation.Id,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var stillRestrained = await verifyContext.Creatures.FindAsync(
            [captive.Id],
            TestContext.Current.CancellationToken
        );
        Assert.True(stillRestrained!.IsRestrained);
        Assert.Equal(_cellLocation.Id, stillRestrained.LocationId);
    }

    [Fact]
    public async Task Handle_IgnoresAJoblessIdleCreature_WhenNotAFreeCreatureObjectiveTarget()
    {
        // Arrange
        var guard = Builders.MakeCreature(WorldId, locationId: _cellLocation.Id);
        _context.Creatures.Add(guard);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new RelocateFreedCaptivesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = _cellLocation.Id,
                GameTime = GameClock.Epoch,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.NotNull(
            await verifyContext.Creatures.FindAsync(
                [guard.Id],
                TestContext.Current.CancellationToken
            )
        );
    }
}
