using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class GrantAllAbilitiesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private GrantAllAbilitiesCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GrantAllAbilitiesCommandHandler(_context);

        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_GrantsEveryNamedAbility_WhenCreatureHasNone()
    {
        // Act
        await _handler.Handle(
            new GrantAllAbilitiesCommand
            {
                WorldId = _creature.WorldId,
                CreatureId = _creature.Id,
                AbilityNames = ["Slash", "Fireball", "Backstab"],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var granted = verifyContext
            .CreatureAbilities.Where(a => a.CreatureId == _creature.Id)
            .Select(a => a.AbilityName)
            .ToArray();
        Assert.Equal(3, granted.Length);
        Assert.Contains("Slash", granted);
        Assert.Contains("Fireball", granted);
        Assert.Contains("Backstab", granted);
    }

    [Fact]
    public async Task Handle_OnlyAddsAbilitiesNotAlreadyOwned()
    {
        // Arrange
        _context.CreatureAbilities.Add(
            new CreatureAbility
            {
                WorldId = _creature.WorldId,
                CreatureId = _creature.Id,
                AbilityName = "Slash",
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new GrantAllAbilitiesCommand
            {
                WorldId = _creature.WorldId,
                CreatureId = _creature.Id,
                AbilityNames = ["Slash", "Fireball"],
            },
            TestContext.Current.CancellationToken
        );

        // Assert — Slash isn't duplicated, and Fireball was added
        await using var verifyContext = db.CreateContext();
        var granted = verifyContext
            .CreatureAbilities.Where(a => a.CreatureId == _creature.Id)
            .Select(a => a.AbilityName)
            .ToArray();
        Assert.Equal(2, granted.Length);
        Assert.Contains("Slash", granted);
        Assert.Contains("Fireball", granted);
    }
}
