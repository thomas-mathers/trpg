using TRPG.Application.Common;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class UpdateCreaturesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private UpdateCreaturesCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(locationId: Guid.NewGuid());

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new UpdateCreaturesCommandHandler(_context);

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_UpdatesLocation_WhenSet()
    {
        // Arrange
        var locationId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id],
                LocationId = Optional<Guid?>.Of(locationId),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(locationId, updated!.LocationId);
    }

    [Fact]
    public async Task Handle_UpdatesState_WhenSet()
    {
        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id],
                State = CreatureState.Training,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Training, updated!.State);
    }

    [Fact]
    public async Task Handle_LeavesLocationUnchanged_WhenNotSet()
    {
        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_creature.Id], State = CreatureState.Busy },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_creature.LocationId, updated!.LocationId);
    }

    [Fact]
    public async Task Handle_UpdatesLastRegenPlaytime_WhenSet()
    {
        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id],
                LastRegenPlaytime = TimeSpan.FromHours(3),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(TimeSpan.FromHours(3), updated!.LastRegenPlaytime);
    }

    [Fact]
    public async Task Handle_LeavesLastRegenPlaytimeUnchanged_WhenNotSet()
    {
        // Arrange
        var originalLastRegenPlaytime = _creature.LastRegenPlaytime;

        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_creature.Id], State = CreatureState.Busy },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(originalLastRegenPlaytime, updated!.LastRegenPlaytime);
    }

    [Fact]
    public async Task Handle_LeavesStateUnchanged_WhenNotSet()
    {
        // Arrange
        var originalState = _creature.State;

        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id],
                LocationId = Optional<Guid?>.Of(Guid.NewGuid()),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(originalState, updated!.State);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoFieldsAreSet()
    {
        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_creature.LocationId, updated!.LocationId);
        Assert.Equal(_creature.State, updated.State);
        Assert.Equal(_creature.LastRegenPlaytime, updated.LastRegenPlaytime);
    }

    [Fact]
    public async Task Handle_UpdatesState_ForEveryCreatureId_WhenGivenMultiple()
    {
        // Arrange
        var otherCreature = Builders.MakeCreature();
        _context.Creatures.Add(otherCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id, otherCreature.Id],
                State = CreatureState.Sitting,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedCreature = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        var updatedOther = await verifyContext.Creatures.FindAsync(
            [otherCreature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Sitting, updatedCreature!.State);
        Assert.Equal(CreatureState.Sitting, updatedOther!.State);
    }

    [Fact]
    public async Task Handle_CanSetLocationIdToNull_WhenExplicitlySet()
    {
        // Arrange
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id],
                LocationId = Optional<Guid?>.Of(Guid.NewGuid()),
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id],
                LocationId = Optional<Guid?>.Of(null),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Null(updated!.LocationId);
    }
}
