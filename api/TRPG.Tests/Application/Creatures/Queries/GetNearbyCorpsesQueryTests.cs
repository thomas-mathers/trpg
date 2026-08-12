using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetNearbyCorpsesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    // Instance (not static) fields: each [Fact] gets its own xUnit class instance, so fresh
    // Guids here keep every test's location isolated from every other test's seeded
    // creatures — there's no transaction rollback between tests on the shared Postgres container.
    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetNearbyCorpsesQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetNearbyCorpsesQueryHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyDeadCreatures_AtThePlayersLocation()
    {
        // Arrange
        var location = Builders.MakeLocation(_worldId);
        var player = Builders.MakeCreature(_worldId, locationId: location.Id);
        var corpse = Builders.MakeCreature(
            _worldId,
            locationId: location.Id,
            state: CreatureState.Dead,
            name: "Corpse"
        );
        var livingCreature = Builders.MakeCreature(
            _worldId,
            locationId: location.Id,
            name: "Living"
        );
        _context.Locations.Add(location);
        _context.Creatures.AddRange(player, corpse, livingCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCorpsesQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var found = Assert.Single(result);
        Assert.Equal(corpse.Id, found.Id);
    }

    [Fact]
    public async Task Handle_ExcludesTheQueryingPlayer()
    {
        // Arrange — the player themselves is dead too, but must never appear in their own list
        var location = Builders.MakeLocation(_worldId);
        var player = Builders.MakeCreature(
            _worldId,
            locationId: location.Id,
            state: CreatureState.Dead
        );
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCorpsesQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ExcludesCorpsesAtADifferentLocation()
    {
        // Arrange
        var location = Builders.MakeLocation(_worldId);
        var otherLocation = Builders.MakeLocation(_worldId);
        var player = Builders.MakeCreature(_worldId, locationId: location.Id);
        var farCorpse = Builders.MakeCreature(
            _worldId,
            locationId: otherLocation.Id,
            state: CreatureState.Dead
        );
        _context.Locations.AddRange(location, otherLocation);
        _context.Creatures.AddRange(player, farCorpse);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCorpsesQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
