using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureAbilitiesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetCreatureAbilitiesQueryHandler _handler = null!;
    private static readonly Guid WorldId = Guid.NewGuid();
    private readonly Creature _player = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureAbilitiesQueryHandler(_context);

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_IncludesStrike_EvenWithNoLearnedAbilities()
    {
        // Act
        var abilities = await _handler.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(abilities, a => a.Name == "Strike");
    }

    [Fact]
    public async Task Handle_ExcludesAbilities_WhenCreatureHasNoSkills()
    {
        // Act — unlike Strike, Block is a normal learned ability, not hardcoded onto everyone
        var abilities = await _handler.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(abilities, a => a.Name == "Block");
    }

    [Fact]
    public async Task Handle_IncludesAbilitiesUnlockedByCreatureSkills()
    {
        // Arrange
        _context.CreatureSkills.Add(
            new CreatureSkill
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                Skill = Skill.Melee,
                Level = 2,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var abilities = await _handler.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(abilities, a => a.Name == "Cleave");
        Assert.Contains(abilities, a => a.Name == "Strike");
    }

    [Fact]
    public async Task Handle_ExcludesAbilitiesUnlockedByOtherCreaturesSkills()
    {
        // Arrange
        var otherCreature = Builders.MakeCreature(WorldId);
        var skill = new CreatureSkill
        {
            WorldId = WorldId,
            CreatureId = otherCreature.Id,
            Skill = Skill.Melee,
            Level = 1,
        };
        _context.Creatures.Add(otherCreature);
        _context.CreatureSkills.Add(skill);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var abilities = await _handler.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.DoesNotContain(abilities, a => a.Name == "Slash");
    }
}
