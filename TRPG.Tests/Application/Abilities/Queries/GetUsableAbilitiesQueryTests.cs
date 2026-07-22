using TRPG.Application.Abilities;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.Abilities.Queries;

[Collection("Database")]
public sealed class GetUsableAbilitiesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetUsableAbilitiesQueryHandler _handler = null!;
    private static readonly Guid WorldId = Guid.NewGuid();
    private readonly Creature _player = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetUsableAbilitiesQueryHandler(
            new GetCreatureAbilitiesQueryHandler(_context),
            AbilityDefinitions.Create()
        );

        await _context.AddCreature(_player, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task AddLearnedAbility(Guid creatureId, string abilityName)
    {
        _context.CreatureAbilities.Add(
            new CreatureAbility
            {
                WorldId = WorldId,
                CreatureId = creatureId,
                AbilityName = abilityName,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_IncludesStrike_EvenWithNoLearnedAbilities()
    {
        // Act
        var abilities = await _handler.Handle(
            new GetUsableAbilitiesQuery { CreatureId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(abilities, a => a.Name == "Strike");
    }

    [Fact]
    public async Task Handle_IncludesBlock_EvenWithNoLearnedAbilities()
    {
        // Act
        var abilities = await _handler.Handle(
            new GetUsableAbilitiesQuery { CreatureId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(abilities, a => a.Name == "Block");
    }

    [Fact]
    public async Task Handle_IncludesLearnedAbilities()
    {
        // Arrange
        await AddLearnedAbility(_player.Id, "Slash");

        // Act
        var abilities = await _handler.Handle(
            new GetUsableAbilitiesQuery { CreatureId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(abilities, a => a.Name == "Slash");
        Assert.Contains(abilities, a => a.Name == "Strike");
    }

    [Fact]
    public async Task Handle_ExcludesAbilities_LearnedByOtherCreatures()
    {
        // Arrange
        var otherCreature = await _context.AddCreature(
            Builders.MakeCreature(WorldId),
            TestContext.Current.CancellationToken
        );
        await AddLearnedAbility(otherCreature.Id, "Slash");

        // Act
        var abilities = await _handler.Handle(
            new GetUsableAbilitiesQuery { CreatureId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(abilities, a => a.Name == "Slash");
    }
}
