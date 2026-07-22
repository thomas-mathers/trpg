using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Scenes.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Queries;

[Collection("Database")]
public sealed class GetNamedEntitiesByWorldQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetNamedEntitiesByWorldQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        _handler = new GetNamedEntitiesByWorldQueryHandler(_context, cache);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Handle_ReturnsCreaturesBuildingsAndDistricts_InTheWorld()
    {
        // Arrange
        var creature = Builders.MakeCreature(WorldId);
        var building = Builders.MakeBuilding(Guid.NewGuid(), worldId: WorldId);
        var district = Builders.MakeDistrict(Guid.NewGuid(), worldId: WorldId);
        _context.Creatures.Add(creature);
        _context.Buildings.Add(building);
        _context.Districts.Add(district);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNamedEntitiesByWorldQuery { WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, e => e.Name == creature.Name && e.Type == NamedEntityType.Creature);
        Assert.Contains(result, e => e.Name == building.Name && e.Type == NamedEntityType.Building);
        Assert.Contains(result, e => e.Name == district.Name && e.Type == NamedEntityType.District);
    }

    [Fact]
    public async Task Handle_ExcludesEntities_FromOtherWorlds()
    {
        // Arrange
        var otherWorldId = Guid.NewGuid();
        var creatureHere = Builders.MakeCreature(WorldId, name: $"Here-{Guid.NewGuid():N}");
        var creatureElsewhere = Builders.MakeCreature(
            otherWorldId,
            name: $"Elsewhere-{Guid.NewGuid():N}"
        );
        _context.Creatures.AddRange(creatureHere, creatureElsewhere);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNamedEntitiesByWorldQuery { WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, e => e.Name == creatureHere.Name);
        Assert.DoesNotContain(result, e => e.Name == creatureElsewhere.Name);
    }

    [Fact]
    public async Task Handle_ReturnsWorldCountryStateAndCity_ForTheWorld()
    {
        // Arrange
        var world = Builders.MakeWorld();
        var country = Builders.MakeCountry(world.Id);
        var state = Builders.MakeState(country.Id, world.Id);
        var city = Builders.MakeCity(state.Id, country.Id, worldId: world.Id);
        _context.Worlds.Add(world);
        _context.Countries.Add(country);
        _context.States.Add(state);
        _context.Cities.Add(city);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNamedEntitiesByWorldQuery { WorldId = world.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, e => e.Name == world.Name && e.Type == NamedEntityType.World);
        Assert.Contains(result, e => e.Name == country.Name && e.Type == NamedEntityType.Country);
        Assert.Contains(result, e => e.Name == state.Name && e.Type == NamedEntityType.State);
        Assert.Contains(result, e => e.Name == city.Name && e.Type == NamedEntityType.City);
    }

    [Fact]
    public async Task Handle_IncludesUniqueItems_ButExcludesOtherRarities()
    {
        // Arrange
        var uniqueItem = Builders.MakeItem(WorldId, ItemRarity.Unique);
        var normalItem = Builders.MakeItem(WorldId, ItemRarity.Normal);
        _context.Items.AddRange(uniqueItem, normalItem);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNamedEntitiesByWorldQuery { WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(result, e => e.Name == uniqueItem.Name && e.Type == NamedEntityType.Item);
        Assert.DoesNotContain(result, e => e.Name == normalItem.Name);
    }
}
