using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters;

[Collection("Database")]
public sealed class WrongedFactionResolverTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _cityId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private WrongedFactionResolver _resolver = null!;
    private Location _location = null!;
    private Faction _cityFaction = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _resolver = _serviceProvider.GetRequiredService<WrongedFactionResolver>();

        _location = Builders.MakeLocation(WorldId, Guid.NewGuid(), cityId: _cityId);
        _cityFaction = Builders.MakeFaction(WorldId, isCityFaction: true, cityId: _cityId);

        _context.Locations.Add(_location);
        _context.Factions.Add(_cityFaction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Resolve_WrongsTheCity_WhenNobodyOwnsTheBuilding()
    {
        // Arrange — almost every building has no owner, which is why this is the common case.

        // Act
        var factionIds = await _resolver.Resolve(
            _location.Id,
            ownerFactionId: null,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([_cityFaction.Id], factionIds);
    }

    [Fact]
    public async Task Resolve_WrongsTheOwnerAndTheCity_WhenAGuildHoldsTheBuilding()
    {
        // Arrange
        var guild = Builders.MakeFaction(WorldId);
        _context.Factions.Add(guild);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var factionIds = await _resolver.Resolve(
            _location.Id,
            guild.Id,
            TestContext.Current.CancellationToken
        );

        // Assert — the owner comes first, so a guild hall is defended by its guild.
        Assert.Equal([guild.Id, _cityFaction.Id], factionIds);
    }

    [Fact]
    public async Task Resolve_WrongsNobody_WhenTheCrimeHappenedOutsideAnyCity()
    {
        // Arrange
        var wilderness = Builders.MakeLocation(WorldId, Guid.NewGuid());
        _context.Locations.Add(wilderness);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var factionIds = await _resolver.Resolve(
            wilderness.Id,
            ownerFactionId: null,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(factionIds);
    }
}
