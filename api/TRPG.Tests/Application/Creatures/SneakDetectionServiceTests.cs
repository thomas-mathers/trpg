using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures;

[Collection("Database")]
public sealed class SneakDetectionServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly SkillCheckCurve Curve = new(
        BaseChance: 0.5f,
        ChanceChangePerSkillLevel: 0f,
        MinimumChance: 0f,
        MaximumChance: 1f
    );

    private readonly TestChanceRoller _chanceRoller = new();
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private ServiceProvider _serviceProvider = null!;
    private SneakDetectionService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        _context.GameSessions.Add(Builders.MakeGameSession(_creature.WorldId, _creature.Id));
        _context.CreatureSkills.Add(
            Builders.MakeCreatureSkill(
                _creature.Id,
                Skill.Sneak,
                level: 1,
                worldId: _creature.WorldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .RemoveAll<IChanceRoller>()
            .AddSingleton<IChanceRoller>(_chanceRoller)
            .BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<SneakDetectionService>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task RollDetection_ReturnsTrueWithoutRolling_WhenNotSneaking()
    {
        // Arrange
        _chanceRoller.Result = false;

        // Act
        var isDetected = await _service.RollDetection(
            _creature.WorldId,
            _creature.Id,
            isSneaking: false,
            Curve,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(isDetected);
        Assert.False(_chanceRoller.WasCalled);
    }

    [Fact]
    public async Task RollDetection_GrantsSneakExperience_WhenSneakingAndUndetected()
    {
        // Arrange
        _chanceRoller.Result = false;

        // Act
        var isDetected = await _service.RollDetection(
            _creature.WorldId,
            _creature.Id,
            isSneaking: true,
            Curve,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(isDetected);
        await using var verifyContext = db.CreateContext();
        var sneak = await verifyContext.CreatureSkills.SingleAsync(
            s => s.CreatureId == _creature.Id && s.Skill == Skill.Sneak,
            TestContext.Current.CancellationToken
        );
        Assert.True(sneak.Experience > 0);
    }

    [Fact]
    public async Task RollDetection_ClearsIsSneaking_WhenSneakingAndDetected()
    {
        // Arrange
        _creature.IsSneaking = true;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var isDetected = await _service.RollDetection(
            _creature.WorldId,
            _creature.Id,
            isSneaking: true,
            Curve,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(isDetected);
        await using var verifyContext = db.CreateContext();
        var updatedCreature = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(updatedCreature.IsSneaking);
    }

    private sealed class TestChanceRoller : IChanceRoller
    {
        public bool Result { get; set; }
        public bool WasCalled { get; private set; }

        public bool Roll(float chance)
        {
            WasCalled = true;
            return Result;
        }
    }
}
