using Microsoft.EntityFrameworkCore;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class AdjustCreatureSkillsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private AdjustCreatureSkillsCommandHandler _handler = null!;
    private Guid _worldId;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new AdjustCreatureSkillsCommandHandler(
            _context,
            new TestOptionsSnapshot<CreatureGeneratorOptions>(new CreatureGeneratorOptions())
        );

        _worldId = _creature.WorldId;
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task<CreatureSkill> SeedSkill(Skill skill, int level, int experience)
    {
        var creatureSkill = new CreatureSkill
        {
            WorldId = _worldId,
            CreatureId = _creature.Id,
            Skill = skill,
            Level = level,
            Experience = experience,
        };
        _context.CreatureSkills.Add(creatureSkill);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creatureSkill;
    }

    private async Task<Creature> ReloadCreature()
    {
        await using var freshContext = db.CreateContext();
        return (
            await freshContext.Creatures.FindAsync(
                [_creature.Id],
                TestContext.Current.CancellationToken
            )
        )!;
    }

    [Fact]
    public async Task Handle_AddsExperience_WithoutLevelingUp_WhenBelowThreshold()
    {
        // Arrange — level 1 needs 250 xp for level 2; starting at 100, one use (+10) isn't enough
        await SeedSkill(Skill.Swordsmanship, level: 1, experience: 100);

        // Act
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int> { [Skill.Swordsmanship] = 1 },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var skill = await _context.CreatureSkills.SingleAsync(
            s => s.CreatureId == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(110, skill.Experience);
        Assert.Equal(1, skill.Level);
    }

    [Fact]
    public async Task Handle_LevelsUpSkill_WhenThresholdCrossed()
    {
        // Arrange — level 2 threshold is 250 xp; 240 + 2 uses (+20) crosses it
        await SeedSkill(Skill.Swordsmanship, level: 1, experience: 240);

        // Act
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int> { [Skill.Swordsmanship] = 2 },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var skill = await _context.CreatureSkills.SingleAsync(
            s => s.CreatureId == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(260, skill.Experience);
        Assert.Equal(2, skill.Level);
    }

    [Fact]
    public async Task Handle_DerivesCharacterLevel_FromSkillContribution_WhenASkillLevelsUp()
    {
        // Arrange — one skill-level gained (contributing CalculateExperienceFromSkillLevel(2) = 2
        // xp toward character level) falls short of CalculateExperienceFromLevel(2) = 3, so the
        // creature's derived level stays at 1
        await SeedSkill(Skill.Swordsmanship, level: 1, experience: 240);

        // Act
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int> { [Skill.Swordsmanship] = 2 },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var creature = await ReloadCreature();
        Assert.Equal(1, creature.Level);
    }

    [Fact]
    public async Task Handle_SumsContributionsAcrossAllSkills_WhenMultipleSkillsLevelUpInOneCall()
    {
        // Arrange — both skills cross from level 1 to 2 this round, contributing
        // CalculateExperienceFromSkillLevel(2) = 2 each, and 2 + 2 = 4 clears
        // CalculateExperienceFromLevel(2) = 3, proving the two skills' contributions are summed
        // together rather than checked independently (either alone, at 2, would fall short)
        await SeedSkill(Skill.Swordsmanship, level: 1, experience: 140);
        await SeedSkill(Skill.Warfare, level: 1, experience: 140);

        // Act
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int>
                {
                    [Skill.Swordsmanship] = 1,
                    [Skill.Warfare] = 1,
                },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var creature = await ReloadCreature();
        Assert.Equal(2, creature.Level);
    }

    [Fact]
    public async Task Handle_DoesNotChangeCharacterLevel_WhenNoSkillLevelsUp()
    {
        // Arrange — partial skill progress alone must never move character level
        await SeedSkill(Skill.Swordsmanship, level: 1, experience: 100);

        // Act
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int> { [Skill.Swordsmanship] = 1 },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var creature = await ReloadCreature();
        Assert.Equal(1, creature.Level);
    }

    [Fact]
    public async Task Handle_SkipsSkill_WhenNoMatchingCreatureSkillRowExists()
    {
        // Act — Stealth was never seeded for this creature
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int> { [Skill.Stealth] = 5 },
            },
            TestContext.Current.CancellationToken
        );

        // Assert — no row created, no exception
        var skills = await _context
            .CreatureSkills.Where(s => s.CreatureId == _creature.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Empty(skills);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenUsageCountsIsEmpty()
    {
        // Arrange
        await SeedSkill(Skill.Swordsmanship, level: 1, experience: 100);

        // Act
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int>(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var skill = await _context.CreatureSkills.SingleAsync(
            s => s.CreatureId == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(100, skill.Experience);
    }
}
