using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

[Collection("Database")]
public sealed class SetWorldPlaytimeCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private SetWorldPlaytimeCommandHandler _handler = null!;
    private readonly World _world = Builders.MakeWorld();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new SetWorldPlaytimeCommandHandler(_context);

        _context.Worlds.Add(_world);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PersistsPlaytime()
    {
        // Act
        await _handler.Handle(
            new SetWorldPlaytimeCommand { WorldId = _world.Id, Playtime = TimeSpan.FromHours(5) },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Worlds.FindAsync(
            [_world.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(TimeSpan.FromHours(5), updated!.Playtime);
    }
}
