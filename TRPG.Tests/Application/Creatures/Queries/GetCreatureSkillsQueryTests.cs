using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureSkillsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetCreatureSkillsQueryHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureSkillsQueryHandler(_context);

        await _context.AddCreature(_creature, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ComputesExperienceProgress_ForEachLearnedSkill()
    {
        // Arrange — level 2 floor is XpForSkillLevel(2) = 250, next level floor is
        // XpForSkillLevel(3) = 450, so experience 300 sits at Current = 50, ToNextLevel = 200.
        _context.CreatureSkills.Add(
            new CreatureSkill
            {
                WorldId = _creature.WorldId,
                CreatureId = _creature.Id,
                Skill = Skill.Swordsmanship,
                Level = 2,
                Experience = 300,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var skills = await _handler.Handle(
            new GetCreatureSkillsQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        var skill = Assert.Single(skills);
        Assert.Equal(Skill.Swordsmanship, skill.Skill);
        Assert.Equal(2, skill.Level);
        Assert.Equal(50, skill.ExperienceCurrent);
        Assert.Equal(200, skill.ExperienceToNextLevel);
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenCreatureHasNoSkills()
    {
        // Act
        var skills = await _handler.Handle(
            new GetCreatureSkillsQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(skills);
    }
}
