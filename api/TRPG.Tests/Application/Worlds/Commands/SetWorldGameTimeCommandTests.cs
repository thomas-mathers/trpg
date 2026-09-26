using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

public sealed class SetWorldGameTimeCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private SetWorldGameTimeCommandHandler _handler = null!;
    private readonly World _world = Builders.MakeWorld();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new SetWorldGameTimeCommandHandler(_context);

        _context.Worlds.Add(_world);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PersistsGameTime()
    {
        // Act
        await _handler.Handle(
            new SetWorldGameTimeCommand
            {
                WorldId = _world.Id,
                GameTime = GameClock.Epoch + TimeSpan.FromHours(5),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Worlds.FindAsync(
            [_world.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(5), updated!.GameTime);
    }
}
