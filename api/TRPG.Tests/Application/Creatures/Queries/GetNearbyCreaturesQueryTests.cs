using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetNearbyCreaturesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetNearbyCreaturesQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetNearbyCreaturesQueryHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_IncludesTheAnchorCreatureItself()
    {
        // Arrange - GetSceneQueryHandler relies on this to fold the player's own row into the
        // same result set instead of fetching it separately
        var location = Builders.MakeLocation(WorldId);
        var player = Builders.MakeCreature(WorldId, locationId: location.Id);
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCreaturesQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, x => x.Id == player.Id);
    }

    [Fact]
    public async Task Handle_ReturnsCreaturesAtTheSameLocation()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId);
        var elsewhereLocation = Builders.MakeLocation(WorldId);
        var player = Builders.MakeCreature(WorldId, locationId: location.Id);
        var atLocation = Builders.MakeCreature(WorldId, locationId: location.Id);
        var elsewhere = Builders.MakeCreature(WorldId, locationId: elsewhereLocation.Id);
        _context.Locations.AddRange(location, elsewhereLocation);
        _context.Creatures.AddRange(player, atLocation, elsewhere);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCreaturesQuery { PlayerId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, x => x.Id == atLocation.Id);
        Assert.DoesNotContain(result, x => x.Id == elsewhere.Id);
    }

    [Fact]
    public async Task Handle_ExcludesGivenCreature()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId);
        var player = Builders.MakeCreature(WorldId, locationId: location.Id);
        var other = Builders.MakeCreature(WorldId, locationId: location.Id);
        _context.Locations.Add(location);
        _context.Creatures.AddRange(player, other);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCreaturesQuery { PlayerId = player.Id, ExcludingCreatureId = player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(result, x => x.Id == player.Id);
        Assert.Contains(result, x => x.Id == other.Id);
    }

    [Fact]
    public async Task Handle_ExcludesDeadCreatures_WhenIncludeDeadIsFalse()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId);
        var player = Builders.MakeCreature(WorldId, locationId: location.Id);
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: location.Id,
            state: CreatureState.Dead
        );
        _context.Locations.Add(location);
        _context.Creatures.AddRange(player, corpse);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCreaturesQuery { PlayerId = player.Id, IncludeDead = false },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(result, x => x.Id == corpse.Id);
        Assert.Contains(result, x => x.Id == player.Id);
    }

    [Fact]
    public async Task Handle_FiltersByCreatureTypes_WhenProvided()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId);
        var player = Builders.MakeCreature(WorldId, locationId: location.Id);
        var goblin = Builders.MakeCreature(
            WorldId,
            locationId: location.Id,
            creatureType: CreatureType.Goblin
        );
        _context.Locations.Add(location);
        _context.Creatures.AddRange(player, goblin);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNearbyCreaturesQuery
            {
                PlayerId = player.Id,
                CreatureTypes = [CreatureType.Goblin],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(result, x => x.Id == player.Id);
        Assert.Contains(result, x => x.Id == goblin.Id);
    }
}
