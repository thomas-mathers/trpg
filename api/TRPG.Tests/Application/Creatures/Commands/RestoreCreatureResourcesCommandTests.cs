using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class RestoreCreatureResourcesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RestoreCreatureResourcesCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(
        WorldId,
        currentHp: 1,
        currentAp: 0,
        currentMp: 0
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<RestoreCreatureResourcesCommandHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RestoresHpApAndMp_ToTheirMaximums()
    {
        // Act
        await _handler.Handle(
            new RestoreCreatureResourcesCommand { CreatureIds = [_creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var restored = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(restored.MaximumHp, restored.CurrentHp);
        Assert.Equal(restored.MaximumAp, restored.CurrentAp);
        Assert.Equal(restored.MaximumMp, restored.CurrentMp);
    }

    [Fact]
    public async Task Handle_LeavesOtherCreaturesUnaffected()
    {
        // Arrange
        var other = Builders.MakeCreature(WorldId, currentHp: 1, currentAp: 0, currentMp: 0);
        _context.Creatures.Add(other);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new RestoreCreatureResourcesCommand { CreatureIds = [_creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var untouched = await verifyContext.Creatures.SingleAsync(
            c => c.Id == other.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, untouched.CurrentHp);
        Assert.Equal(0, untouched.CurrentAp);
        Assert.Equal(0, untouched.CurrentMp);
    }
}
