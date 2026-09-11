using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class AdjustCreatureSkillsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AdjustCreatureSkillsCommandHandler _handler = null!;
    private Guid _worldId;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AdjustCreatureSkillsCommandHandler>();

        _worldId = _creature.WorldId;
        _context.Creatures.Add(_creature);
        _context.GameSessions.Add(Builders.MakeGameSession(_worldId, _creature.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<CreatureSkill> SeedSkill(
        Skill skill,
        int level,
        int experience,
        int seedExperience = 0
    )
    {
        var creatureSkill = new CreatureSkill
        {
            WorldId = _worldId,
            CreatureId = _creature.Id,
            Skill = skill,
            Level = level,
            Experience = experience,
            SeedExperience = seedExperience,
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
        await SeedSkill(Skill.Melee, level: 1, experience: 100);

        // Act
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int> { [Skill.Melee] = 1 },
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
        await SeedSkill(Skill.Melee, level: 1, experience: 240);

        // Act
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int> { [Skill.Melee] = 2 },
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
    public async Task Handle_DoesNotCountSeedExperience_TowardSkillLevelUps()
    {
        await SeedSkill(Skill.Melee, level: 1, experience: 150, seedExperience: 150);

        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int> { [Skill.Melee] = 1 },
            },
            TestContext.Current.CancellationToken
        );

        var skill = await _context.CreatureSkills.SingleAsync(
            item => item.CreatureId == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(160, skill.Experience);
        Assert.Equal(1, skill.Level);
    }

    [Fact]
    public async Task Handle_DerivesLevelFromAllSkills_NotJustTheSkillsUsedThisRound()
    {
        // Arrange — an untouched Melee 10 contributes CalculateExperienceFromSkillLevel(10) = 54
        // xp toward character level; General crossing to level 2 adds 2 more. The 56 total meets
        // the level-8 threshold (56), which only holds if the derivation counts skills absent
        // from UsageCounts
        await SeedSkill(Skill.Melee, level: 10, experience: 0);
        await SeedSkill(Skill.General, level: 1, experience: 140);

        // Act
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int> { [Skill.General] = 1 },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var creature = await ReloadCreature();
        Assert.Equal(8, creature.Level);
    }

    [Fact]
    public async Task Handle_SkipsSkill_WhenNoMatchingCreatureSkillRowExists()
    {
        // Act — Sneak was never seeded for this creature
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int> { [Skill.Sneak] = 5 },
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
        await SeedSkill(Skill.Melee, level: 1, experience: 100);

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

    [Fact]
    public async Task Handle_GrantsBoostedExperience_WhenCreatureIsRested()
    {
        // Arrange
        await SeedSkill(Skill.Melee, level: 1, experience: 100);
        var trackedCreature = await _context.Creatures.SingleAsync(
            c => c.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        trackedCreature.RestedUntilPlaytime = TimeSpan.FromHours(24);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = _worldId,
                CreatureId = _creature.Id,
                UsageCounts = new Dictionary<Skill, int> { [Skill.Melee] = 1 },
            },
            TestContext.Current.CancellationToken
        );

        // Assert — banker's rounding takes 1*10*1.25=12.5 to 12 (nearest even), so 100+12=112
        var skill = await _context.CreatureSkills.SingleAsync(
            s => s.CreatureId == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(112, skill.Experience);
    }
}
