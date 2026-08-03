using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs.Commands;

[Collection("Database")]
public sealed class ExecuteCreatureJobCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ExecuteCreatureJobCommandHandler _handler = null!;
    private GetCreaturesAtLocationQueryHandler _getCreaturesAtLocation = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ExecuteCreatureJobCommandHandler>();
        _getCreaturesAtLocation =
            _serviceProvider.GetRequiredService<GetCreaturesAtLocationQueryHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForSleepJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Sleep, CreatureState.Sleeping);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForWorkJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Work, CreatureState.Busy);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForIdleJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Idle, CreatureState.Idle);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForStudyJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Study, CreatureState.Studying);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForPrayJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Pray, CreatureState.Praying);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForTrainJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Train, CreatureState.Training);
    }

    [Fact]
    public async Task Handle_UpdatesCreatureRoomIdAndState_ForSitJob()
    {
        await AssertRoomIdUpdated(CreatureJobAction.Sit, CreatureState.Sitting);
    }

    [Fact]
    public async Task Handle_KeepsCreatureDiscoverableByDistrict_AfterAJobMovesItAcrossDistricts()
    {
        // Arrange
        var oldDistrictId = Guid.NewGuid();
        var newDistrictId = Guid.NewGuid();
        var newRoomId = Guid.NewGuid();
        _creature.DistrictId = oldDistrictId;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentRoomId = _creature.RoomId,
                CurrentState = _creature.State,
                CreatureJobAction = CreatureJobAction.Work,
                JobRoomId = newRoomId,
                JobDistrictId = newDistrictId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var atNewLocation = await _getCreaturesAtLocation.Handle(
            new GetCreaturesAtLocationQuery
            {
                Location = CreatureLocation.Indoor(
                    _creature.WorldId,
                    _creature.StateId,
                    newRoomId,
                    newDistrictId
                ),
            },
            TestContext.Current.CancellationToken
        );
        Assert.Contains(atNewLocation, c => c.Id == _creature.Id);
    }

    [Fact]
    public async Task Handle_LeavesCreatureUnchanged_ForPatrolJob()
    {
        await AssertRoomIdUnchanged(CreatureJobAction.Patrol);
    }

    [Fact]
    public async Task Handle_LeavesCreatureUnchanged_ForSocializeJob()
    {
        await AssertRoomIdUnchanged(CreatureJobAction.Socialize);
    }

    private async Task AssertRoomIdUpdated(CreatureJobAction action, CreatureState expectedState)
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var districtId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentRoomId = _creature.RoomId,
                CurrentState = _creature.State,
                CreatureJobAction = action,
                JobRoomId = roomId,
                JobDistrictId = districtId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(roomId, updated!.RoomId);
        Assert.Equal(districtId, updated.DistrictId);
        Assert.Equal(expectedState, updated.State);
    }

    private async Task AssertRoomIdUnchanged(CreatureJobAction action)
    {
        // Arrange
        var originalRoomId = _creature.RoomId;
        var originalDistrictId = _creature.DistrictId;
        var originalState = _creature.State;

        // Act
        await _handler.Handle(
            new ExecuteCreatureJobCommand
            {
                CreatureId = _creature.Id,
                CurrentRoomId = _creature.RoomId,
                CurrentState = _creature.State,
                CreatureJobAction = action,
                JobRoomId = Guid.NewGuid(),
                JobDistrictId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(originalRoomId, updated!.RoomId);
        Assert.Equal(originalDistrictId, updated.DistrictId);
        Assert.Equal(originalState, updated.State);
    }
}
