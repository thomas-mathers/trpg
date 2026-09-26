using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Queries;

public sealed class GetActiveFightCombatantIdsByWorldQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetActiveFightCombatantIdsByWorldQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetActiveFightCombatantIdsByWorldQueryHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCombatantsOfActiveFights_WhenWorldHasFights()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var enemyId = Guid.NewGuid();
        _context.Encounters.Add(Builders.MakeFight(worldId, playerId, [playerId, enemyId]));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetActiveFightCombatantIdsByWorldQuery { WorldId = worldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(new HashSet<Guid> { playerId, enemyId }, result.ToHashSet());
    }

    [Fact]
    public async Task Handle_ExcludesCombatants_WhenFightIsCompleted()
    {
        // Arrange
        var worldId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var fight = Builders.MakeFight(worldId, playerId, [playerId]);
        fight.State = EncounterState.Completed;
        _context.Encounters.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetActiveFightCombatantIdsByWorldQuery { WorldId = worldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ExcludesCombatants_WhenFightBelongsToAnotherWorld()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        _context.Encounters.Add(Builders.MakeFight(Guid.NewGuid(), playerId, [playerId]));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetActiveFightCombatantIdsByWorldQuery { WorldId = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
