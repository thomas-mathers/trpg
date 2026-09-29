using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

public sealed class GetCreatureConditionsByIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetCreatureConditionsByIdsQueryHandler _handler = null!;
    private readonly Creature _alive = Builders.MakeCreature(WorldId);
    private readonly Creature _dead = Builders.MakeCreature(
        WorldId,
        condition: CreatureCondition.Dead
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetCreatureConditionsByIdsQueryHandler>();

        _context.Creatures.AddRange(_alive, _dead);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsRequestedCreatureConditionsKeyedById()
    {
        // Act
        var result = await _handler.Handle(
            new GetCreatureConditionsByIdsQuery { Ids = [_alive.Id, _dead.Id, Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(CreatureCondition.Awake, result[_alive.Id]);
        Assert.Equal(CreatureCondition.Dead, result[_dead.Id]);
        Assert.Equal(2, result.Count);
    }
}
