using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class ReviveCreatureAtLocationCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid TargetLocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ReviveCreatureAtLocationCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(
        WorldId,
        state: CreatureState.Dead,
        currentHp: 0,
        currentAp: 0,
        currentMp: 0
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ReviveCreatureAtLocationCommandHandler>();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RelocatesAndFullyHealsTheCreature()
    {
        // Arrange
        var command = new ReviveCreatureAtLocationCommand
        {
            CreatureId = _creature.Id,
            LocationId = TargetLocationId,
        };

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var revived = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(TargetLocationId, revived!.LocationId);
        Assert.Equal(CreatureState.Idle, revived.State);
        Assert.Equal(revived.MaximumHp, revived.CurrentHp);
        Assert.Equal(revived.MaximumAp, revived.CurrentAp);
        Assert.Equal(revived.MaximumMp, revived.CurrentMp);
    }
}
