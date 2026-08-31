using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetCityFactionForCreatureQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetCityFactionForCreatureQueryHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetCityFactionForCreatureQueryHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheCityFaction_ExcludingNonCityFactions()
    {
        // Arrange
        var cityFaction = Builders.MakeFaction(WorldId, isCityFaction: true);
        var otherFaction = Builders.MakeFaction(WorldId, isCityFaction: false);
        _context.Factions.AddRange(cityFaction, otherFaction);
        _context.FactionMembers.AddRange(
            Builders.MakeFactionMember(WorldId, cityFaction.Id, _creature.Id),
            Builders.MakeFactionMember(WorldId, otherFaction.Id, _creature.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCityFactionForCreatureQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(cityFaction.Id, result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenCreatureHasNoCityFaction()
    {
        // Act
        var result = await _handler.Handle(
            new GetCityFactionForCreatureQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }
}
