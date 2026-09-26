using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

public sealed class SetDoorTimedLockCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private SetDoorTimedLockCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new SetDoorTimedLockCommandHandler(_context);
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_LocksTheDoorAndSetsTheUnlockTime_WhenGivenAnUnlockTime()
    {
        // Arrange
        var door = Builders.MakeDoorConnector(Guid.NewGuid());
        _context.DoorConnectors.Add(door);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SetDoorTimedLockCommand
            {
                DoorConnectorIds = [door.Id],
                UnlocksAtGameTime = GameClock.Epoch + TimeSpan.FromHours(10),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.DoorConnectors.FindAsync(
            [door.Id],
            TestContext.Current.CancellationToken
        );
        Assert.True(updated!.IsLocked);
        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(10), updated.UnlocksAtGameTime);
    }

    [Fact]
    public async Task Handle_UnlocksTheDoorAndClearsTheUnlockTime_WhenGivenNull()
    {
        // Arrange
        var door = Builders.MakeDoorConnector(
            Guid.NewGuid(),
            isLocked: true,
            unlocksAtGameTime: GameClock.Epoch + TimeSpan.FromHours(10)
        );
        _context.DoorConnectors.Add(door);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SetDoorTimedLockCommand { DoorConnectorIds = [door.Id], UnlocksAtGameTime = null },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.DoorConnectors.FindAsync(
            [door.Id],
            TestContext.Current.CancellationToken
        );
        Assert.False(updated!.IsLocked);
        Assert.Null(updated.UnlocksAtGameTime);
    }
}
