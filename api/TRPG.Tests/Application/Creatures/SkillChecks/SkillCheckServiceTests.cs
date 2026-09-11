using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Creatures.SkillChecks;

public sealed class SkillCheckServiceTests
{
    private readonly CapturingChanceRoller _chanceRoller = new();
    private readonly FakeGetCreatureSkillsQueryHandler _getCreatureSkills = new();
    private readonly SkillCheckService _service;

    public SkillCheckServiceTests() =>
        _service = new SkillCheckService(_getCreatureSkills, _chanceRoller);

    [Fact]
    public async Task Roll_UsesTheSpecifiedCreatureSkillLevel()
    {
        // Arrange — chance = 0.5 + (-0.1) * skillLevel(2) = 0.3
        _getCreatureSkills.Skills = [new CreatureSkillProgress(Skill.Sneak, 2, 0, 0)];

        // Act
        var result = await _service.Roll(
            Guid.NewGuid(),
            Skill.Sneak,
            new SkillCheckCurve(
                BaseChance: 0.5f,
                ChanceChangePerSkillLevel: -0.1f,
                MinimumChance: 0.2f,
                MaximumChance: 0.8f
            ),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result);
        Assert.Equal(0.3f, _chanceRoller.Chance);
    }

    [Fact]
    public async Task Roll_UsesLevelZero_WhenTheCreatureDoesNotHaveTheSkill()
    {
        // Arrange
        _getCreatureSkills.Skills = [];

        // Act
        var result = await _service.Roll(
            Guid.NewGuid(),
            Skill.Pickpocketing,
            new SkillCheckCurve(
                BaseChance: 0.5f,
                ChanceChangePerSkillLevel: -0.1f,
                MinimumChance: 0.2f,
                MaximumChance: 0.8f
            ),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result);
        Assert.Equal(0.5f, _chanceRoller.Chance);
    }

    private sealed class CapturingChanceRoller : IChanceRoller
    {
        public float Chance { get; private set; }

        public bool Roll(float chance)
        {
            Chance = chance;
            return true;
        }
    }

    private sealed class FakeGetCreatureSkillsQueryHandler
        : IQueryHandler<GetCreatureSkillsQuery, IReadOnlyCollection<CreatureSkillProgress>>
    {
        public IReadOnlyCollection<CreatureSkillProgress> Skills { get; set; } = [];

        public Task<IReadOnlyCollection<CreatureSkillProgress>> Handle(
            GetCreatureSkillsQuery query,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Skills);
    }
}
