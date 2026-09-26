using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.LocationSimulation;
using TRPG.Application.LocationSimulation.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Queries;

public sealed class GetActiveLocationPlayersQueryHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetActiveLocationPlayersQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetActiveLocationPlayersQueryHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsPlayerLocationAndLevel_WhenWorldHasPlayer()
    {
        // Arrange
        var world = Builders.MakeWorld();
        var location = Builders.MakeLocation(world.Id);
        var player = Builders.MakeCreature(world.Id, locationId: location.Id, level: 7);
        world.PlayerId = player.Id;
        _context.Worlds.Add(world);
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var players = await _handler.Handle(
            new GetActiveLocationPlayersQuery { WorldId = world.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([new ActiveLocationPlayer(location.Id, player.Id, PlayerLevel: 7)], players);
    }

    [Fact]
    public async Task Handle_ReturnsNothing_WhenWorldHasNoPlayer()
    {
        // Arrange
        var world = Builders.MakeWorld();
        _context.Worlds.Add(world);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var players = await _handler.Handle(
            new GetActiveLocationPlayersQuery { WorldId = world.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(players);
    }
}
