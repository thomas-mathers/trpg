using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.SkillChecks;

[Collection("Database")]
public sealed class SkillCheckServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly CapturingChanceRoller _chanceRoller = new();
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private ServiceProvider _serviceProvider = null!;
    private SkillCheckService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        _context.CreatureSkills.Add(
            new CreatureSkill
            {
                WorldId = _creature.WorldId,
                CreatureId = _creature.Id,
                Skill = Skill.Sneak,
                Level = 2,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .RemoveAll<IChanceRoller>()
            .AddSingleton<IChanceRoller>(_chanceRoller)
            .BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<SkillCheckService>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Roll_UsesTheSpecifiedCreatureSkillLevel()
    {
        var result = await _service.Roll(
            _creature.Id,
            Skill.Sneak,
            new SkillCheckCurve(
                BaseChance: 0.5f,
                ChanceChangePerSkillLevel: -0.1f,
                MinimumChance: 0.2f,
                MaximumChance: 0.8f
            ),
            TestContext.Current.CancellationToken
        );

        Assert.True(result);
        Assert.Equal(0.3f, _chanceRoller.Chance);
    }

    [Fact]
    public async Task Roll_UsesLevelZero_WhenTheCreatureDoesNotHaveTheSkill()
    {
        var result = await _service.Roll(
            _creature.Id,
            Skill.Pickpocketing,
            new SkillCheckCurve(
                BaseChance: 0.5f,
                ChanceChangePerSkillLevel: -0.1f,
                MinimumChance: 0.2f,
                MaximumChance: 0.8f
            ),
            TestContext.Current.CancellationToken
        );

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
}
