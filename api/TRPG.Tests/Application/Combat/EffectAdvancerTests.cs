using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;
using ActiveBuff = TRPG.Application.CreatureFormulas.ActiveBuff;
using ActiveDot = TRPG.Application.Combat.ActiveDot;
using ActiveHot = TRPG.Application.Combat.ActiveHot;

namespace TRPG.Tests.Application.Combat;

public class EffectAdvancerTests
{
    private readonly EffectAdvancer _advancer = new(
        new DamageCalculator(new DefaultOptionsSnapshot<CombatOptions>()),
        CombatTimeScale.Unscaled
    );

    private static Combatant MakeCombatant() =>
        new CombatantBuilder().WithName("Wraith").WithEndurance(100).Build();

    private static ActiveDot MakeDot(int amount, int expiresAfterRounds) =>
        new()
        {
            AbilityName = "Ignite",
            Amount = amount,
            DamageType = DamageType.Fire,
            NextTickAt = TestTime.AfterRounds(1),
            ExpiresAt = TestTime.AfterRounds(expiresAfterRounds),
        };

    private static ActiveHot MakeHot(int amount, int expiresAfterRounds) =>
        new()
        {
            AbilityName = "Regen",
            Amount = amount,
            NextTickAt = TestTime.AfterRounds(1),
            ExpiresAt = TestTime.AfterRounds(expiresAfterRounds),
        };

    private static ActiveBuff MakeBuff(string abilityName, int expiresAfterRounds) =>
        new()
        {
            AbilityName = abilityName,
            Amount = 1,
            AmountType = AmountType.Flat,
            Attribute = AttributeName.Strength,
            ExpiresAt = TestTime.AfterRounds(expiresAfterRounds),
        };

    [Fact]
    public void Advance_AppliesEveryOwedDotTick_WhenSeveralRoundsElapsed()
    {
        // Arrange
        var combatant = MakeCombatant();
        var startingHp = combatant.CurrentHp;
        var dot = MakeDot(amount: 2, expiresAfterRounds: 10);
        combatant.ActiveDots.Add(dot);

        // Act
        var events = _advancer.Advance(combatant, TestTime.AfterRounds(4));

        // Assert
        Assert.Equal(4, events.OfType<DamageTicked>().Count());
        Assert.Equal(startingHp - 8, combatant.CurrentHp);
        Assert.Equal(TestTime.AfterRounds(5), dot.NextTickAt);
    }

    [Fact]
    public void Advance_TicksNothing_BeforeTheFirstTickIsDue()
    {
        // Arrange
        var combatant = MakeCombatant();
        combatant.ActiveDots.Add(MakeDot(amount: 2, expiresAfterRounds: 3));

        // Act
        var events = _advancer.Advance(combatant, TestTime.Start);

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public void Advance_StopsTickingAtExpiry_AndRemovesTheEffect()
    {
        // Arrange
        var combatant = MakeCombatant();
        combatant.ActiveDots.Add(MakeDot(amount: 2, expiresAfterRounds: 3));

        // Act
        var events = _advancer.Advance(combatant, TestTime.AfterRounds(10));

        // Assert
        Assert.Equal(3, events.OfType<DamageTicked>().Count());
        Assert.Empty(combatant.ActiveDots);
    }

    [Fact]
    public void Advance_TicksOncePerScaledRound()
    {
        // Arrange
        var scale = new CombatTimeScale(Options.Create(new WorldClockOptions { TimeScale = 20 }));
        var advancer = new EffectAdvancer(
            new DamageCalculator(new DefaultOptionsSnapshot<CombatOptions>()),
            scale
        );
        var combatant = MakeCombatant();
        combatant.ActiveDots.Add(
            new ActiveDot
            {
                AbilityName = "Ignite",
                Amount = 2,
                DamageType = DamageType.Fire,
                NextTickAt = TestTime.Start + scale.Round,
                ExpiresAt = TestTime.Start + scale.Round * 3,
            }
        );

        // Act
        var events = advancer.Advance(combatant, TestTime.Start + scale.Round * 10);

        // Assert
        Assert.Equal(3, events.OfType<DamageTicked>().Count());
    }

    [Fact]
    public void Advance_StopsCatchingUp_WhenADotKillsTheCombatant()
    {
        // Arrange
        var combatant = new CombatantBuilder().WithEndurance(100).WithCurrentHp(3).Build();
        combatant.ActiveDots.Add(MakeDot(amount: 2, expiresAfterRounds: 10));

        // Act
        var events = _advancer.Advance(combatant, TestTime.AfterRounds(5));

        // Assert
        Assert.Equal(2, events.Count);
        Assert.True(events.OfType<DamageTicked>().Last().Killed);
        Assert.False(combatant.IsAlive);
    }

    [Fact]
    public void Advance_TicksNothing_ForADeadCombatant()
    {
        // Arrange
        var combatant = new CombatantBuilder().WithEndurance(100).WithCurrentHp(0).Build();
        combatant.ActiveDots.Add(MakeDot(amount: 2, expiresAfterRounds: 10));

        // Act
        var events = _advancer.Advance(combatant, TestTime.AfterRounds(5));

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public void Advance_AppliesHealBeforeDamage_WhenBothAreDueAtTheSameInstant()
    {
        // Arrange
        var combatant = new CombatantBuilder().WithEndurance(100).WithCurrentHp(1).Build();
        combatant.ActiveDots.Add(MakeDot(amount: 5, expiresAfterRounds: 10));
        combatant.ActiveHots.Add(MakeHot(amount: 5, expiresAfterRounds: 10));

        // Act
        _advancer.Advance(combatant, TestTime.AfterRounds(1));

        // Assert
        Assert.True(combatant.IsAlive);
        Assert.Equal(1, combatant.CurrentHp);
    }

    [Fact]
    public void Advance_RemovesExpiredEffects_AndKeepsTheRest()
    {
        // Arrange
        var combatant = MakeCombatant();
        combatant.ActiveBuffs.Add(MakeBuff("Expired", expiresAfterRounds: 2));
        combatant.ActiveBuffs.Add(MakeBuff("Lasting", expiresAfterRounds: 5));
        combatant.ActiveConditions[ConditionType.Stunned] = TestTime.AfterRounds(2);
        combatant.ActiveConditions[ConditionType.Blinded] = TestTime.AfterRounds(5);
        combatant.CooldownReadyAtByAbility["Smite"] = TestTime.AfterRounds(2);
        combatant.CooldownReadyAtByAbility["Cleave"] = TestTime.AfterRounds(5);

        // Act
        _advancer.Advance(combatant, TestTime.AfterRounds(2));

        // Assert
        Assert.Equal("Lasting", Assert.Single(combatant.ActiveBuffs).AbilityName);
        Assert.Equal(ConditionType.Blinded, Assert.Single(combatant.ActiveConditions).Key);
        Assert.Equal("Cleave", Assert.Single(combatant.CooldownReadyAtByAbility).Key);
    }
}
