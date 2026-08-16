using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreaturesAtLocationQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetCreaturesAtLocationQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreaturesAtLocationQueryHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCreaturesAtLocation()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId);
        var elsewhereLocation = Builders.MakeLocation(WorldId);
        var atLocation = Builders.MakeCreature(WorldId, locationId: location.Id);
        var elsewhere = Builders.MakeCreature(WorldId, locationId: elsewhereLocation.Id);
        _context.Locations.AddRange(location, elsewhereLocation);
        _context.Creatures.AddRange(atLocation, elsewhere);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCreaturesAtLocationQuery { WorldId = WorldId, LocationId = location.Id },
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
            new GetCreaturesAtLocationQuery
            {
                WorldId = WorldId,
                LocationId = location.Id,
                ExcludingCreatureId = player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(result, x => x.Id == player.Id);
        Assert.Contains(result, x => x.Id == other.Id);
    }

    [Fact]
    public async Task Handle_IncludesDeadCreatures_ByDefault()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId);
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: location.Id,
            state: CreatureState.Dead
        );
        _context.Locations.Add(location);
        _context.Creatures.Add(corpse);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCreaturesAtLocationQuery { WorldId = WorldId, LocationId = location.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, x => x.Id == corpse.Id);
    }

    [Fact]
    public async Task Handle_ReturnsCurrentAndMaximumHp()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId);
        var creature = Builders.MakeCreature(WorldId, locationId: location.Id, currentHp: 5);
        // Simulate gear-boosted cached Maximum diverging from base Attributes.MaximumHp,
        // to prove the query reads the cached column rather than the base value.
        creature.MaximumHp += 50;
        _context.Locations.Add(location);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCreaturesAtLocationQuery { WorldId = WorldId, LocationId = location.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var summary = Assert.Single(result, x => x.Id == creature.Id);
        Assert.Equal(5, summary.CurrentHp);
        Assert.Equal(creature.MaximumHp, summary.MaximumHp);
        Assert.NotEqual(creature.BaseAttributes.MaximumHp, summary.MaximumHp);
    }

    [Fact]
    public async Task Handle_ReturnsNullProfession_WhenCreatureHasNoProfession()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId);
        var minor = Builders.MakeCreature(WorldId, locationId: location.Id, profession: null);
        _context.Locations.Add(location);
        _context.Creatures.Add(minor);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCreaturesAtLocationQuery { WorldId = WorldId, LocationId = location.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var summary = Assert.Single(result, x => x.Id == minor.Id);
        Assert.Null(summary.Profession);
    }

    [Fact]
    public async Task Handle_ExcludesDeadCreatures_WhenIncludeDeadIsFalse()
    {
        // Arrange
        var location = Builders.MakeLocation(WorldId);
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: location.Id,
            state: CreatureState.Dead
        );
        var alive = Builders.MakeCreature(WorldId, locationId: location.Id);
        _context.Locations.Add(location);
        _context.Creatures.AddRange(corpse, alive);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCreaturesAtLocationQuery
            {
                WorldId = WorldId,
                LocationId = location.Id,
                IncludeDead = false,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(result, x => x.Id == corpse.Id);
        Assert.Contains(result, x => x.Id == alive.Id);
    }
}
