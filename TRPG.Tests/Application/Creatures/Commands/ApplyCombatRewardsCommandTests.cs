using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class ApplyCombatRewardsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private ApplyCombatRewardsCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new ApplyCombatRewardsCommandHandler(_context);

        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_AddsExperienceAndGold_ToTheCreature()
    {
        // Act
        await _handler.Handle(
            new ApplyCombatRewardsCommand
            {
                CreatureId = _creature.Id,
                ExperienceGained = 100,
                GoldGained = 25,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(100, updated!.Experience);
        Assert.Equal(25, updated.Gold);
    }

    [Fact]
    public async Task Handle_AccumulatesOnTopOfExistingExperienceAndGold()
    {
        // Arrange
        _creature.Experience = 50;
        _creature.Gold = 10;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ApplyCombatRewardsCommand
            {
                CreatureId = _creature.Id,
                ExperienceGained = 100,
                GoldGained = 25,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(150, updated!.Experience);
        Assert.Equal(35, updated.Gold);
    }
}
