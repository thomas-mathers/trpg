using TRPG.Application.Abilities;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Abilities.Queries;

[Collection("Database")]
public sealed class GetUsableAbilitiesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetUsableAbilitiesQueryHandler _handler = null!;
    private Guid _worldId;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetUsableAbilitiesQueryHandler(
            new GetCreatureAbilitiesQueryHandler(_context),
            AbilityDefinitions.Create()
        );

        _worldId = Guid.NewGuid();
        _player = Builders.MakeCreature(_worldId);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync();
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
                WorldId = _worldId,
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
        var otherCreature = Builders.MakeCreature(_worldId);
        _context.Creatures.Add(otherCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
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
