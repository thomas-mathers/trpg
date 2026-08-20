using TRPG.Application.Buildings.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Buildings.Commands;

[Collection("Database")]
public sealed class SetDoorTimedLockCommandTests(DatabaseFixture db) : IAsyncLifetime
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
                UnlocksAtPlaytime = TimeSpan.FromHours(10),
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
        Assert.Equal(TimeSpan.FromHours(10), updated.UnlocksAtPlaytime);
    }

    [Fact]
    public async Task Handle_UnlocksTheDoorAndClearsTheUnlockTime_WhenGivenNull()
    {
        // Arrange
        var door = Builders.MakeDoorConnector(
            Guid.NewGuid(),
            isLocked: true,
            unlocksAtPlaytime: TimeSpan.FromHours(10)
        );
        _context.DoorConnectors.Add(door);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SetDoorTimedLockCommand { DoorConnectorIds = [door.Id], UnlocksAtPlaytime = null },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.DoorConnectors.FindAsync(
            [door.Id],
            TestContext.Current.CancellationToken
        );
        Assert.False(updated!.IsLocked);
        Assert.Null(updated.UnlocksAtPlaytime);
    }
}
