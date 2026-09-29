using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Effects;
using TRPG.Application.Effects.Results;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;
using ActiveBuff = TRPG.Application.CreatureFormulas.ActiveBuff;
using ActiveDot = TRPG.Application.Effects.ActiveDot;
using ActiveHot = TRPG.Application.Effects.ActiveHot;

namespace TRPG.Tests.Application.Effects;

public class EffectAdvancerTests
{
    private readonly EffectAdvancer _advancer = new(
        new DefaultOptionsSnapshot<CombatOptions>(),
        EffectTimeScale.Unscaled
    );

    private static EffectState MakeState(int currentHp = 1000, float fireResistance = 0) =>
        new()
        {
            CurrentHp = currentHp,
            Attributes = new Attributes { MaximumHp = 1000, FireResistance = fireResistance },
        };

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
        var state = MakeState();
        var startingHp = state.CurrentHp;
        var dot = MakeDot(amount: 2, expiresAfterRounds: 10);
        state.ActiveDots.Add(dot);

        // Act
        var events = _advancer.Advance(state, TestTime.AfterRounds(4));

        // Assert
        Assert.Equal(4, events.Ticks.OfType<DamageEffectTick>().Count());
        Assert.Equal(startingHp - 8, state.CurrentHp);
        Assert.Equal(TestTime.AfterRounds(5), dot.NextTickAt);
    }

    [Fact]
    public void Advance_TicksNothing_BeforeTheFirstTickIsDue()
    {
        // Arrange
        var state = MakeState();
        state.ActiveDots.Add(MakeDot(amount: 2, expiresAfterRounds: 3));

        // Act
        var events = _advancer.Advance(state, TestTime.Start);

        // Assert
        Assert.Empty(events.Ticks);
        Assert.False(events.Changed);
    }

    [Fact]
    public void Advance_StopsTickingAtExpiry_AndRemovesTheEffect()
    {
        // Arrange
        var state = MakeState();
        state.ActiveDots.Add(MakeDot(amount: 2, expiresAfterRounds: 3));

        // Act
        var events = _advancer.Advance(state, TestTime.AfterRounds(10));

        // Assert
        Assert.Equal(3, events.Ticks.OfType<DamageEffectTick>().Count());
        Assert.Empty(state.ActiveDots);
    }

    [Fact]
    public void Advance_TicksOncePerScaledRound()
    {
        // Arrange
        var scale = new EffectTimeScale(Options.Create(new WorldClockOptions { TimeScale = 20 }));
        var advancer = new EffectAdvancer(new DefaultOptionsSnapshot<CombatOptions>(), scale);
        var state = MakeState();
        state.ActiveDots.Add(
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
        var events = advancer.Advance(state, TestTime.Start + scale.Round * 10);

        // Assert
        Assert.Equal(3, events.Ticks.OfType<DamageEffectTick>().Count());
    }

    [Fact]
    public void Advance_StopsCatchingUp_WhenADotKillsTheCreature()
    {
        // Arrange
        var state = MakeState(currentHp: 3);
        state.ActiveDots.Add(MakeDot(amount: 2, expiresAfterRounds: 10));

        // Act
        var events = _advancer.Advance(state, TestTime.AfterRounds(5));

        // Assert
        Assert.Equal(2, events.Ticks.Count);
        Assert.True(events.Ticks.OfType<DamageEffectTick>().Last().Killed);
        Assert.False(state.IsAlive);
    }

    [Fact]
    public void Advance_TicksNothing_ForADeadCreature()
    {
        // Arrange
        var state = MakeState(currentHp: 0);
        state.ActiveDots.Add(MakeDot(amount: 2, expiresAfterRounds: 10));

        // Act
        var events = _advancer.Advance(state, TestTime.AfterRounds(5));

        // Assert
        Assert.Empty(events.Ticks);
        Assert.False(events.Changed);
    }

    [Fact]
    public void Advance_AppliesHealBeforeDamage_WhenBothAreDueAtTheSameInstant()
    {
        // Arrange
        var state = MakeState(currentHp: 1);
        state.ActiveDots.Add(MakeDot(amount: 5, expiresAfterRounds: 10));
        state.ActiveHots.Add(MakeHot(amount: 5, expiresAfterRounds: 10));

        // Act
        _advancer.Advance(state, TestTime.AfterRounds(1));

        // Assert
        Assert.True(state.IsAlive);
        Assert.Equal(1, state.CurrentHp);
    }

    [Fact]
    public void Advance_ReportsAChangeWithoutATick_WhenOnlyAConditionExpires()
    {
        // Arrange
        var state = MakeState();
        state.ActiveConditions[ConditionType.Stunned] = TestTime.AfterRounds(1);

        // Act
        var result = _advancer.Advance(state, TestTime.AfterRounds(1));

        // Assert
        Assert.True(result.Changed);
        Assert.Empty(result.Ticks);
        Assert.Empty(state.ActiveConditions);
    }

    [Theory]
    [InlineData(0.5f, 10)]
    [InlineData(1f, 5)]
    public void Advance_MitigatesDamageUsingTheConfiguredCap(float resistance, int expectedDamage)
    {
        // Arrange
        var state = MakeState(fireResistance: resistance);
        state.ActiveDots.Add(MakeDot(amount: 20, expiresAfterRounds: 3));
        var advancer = new EffectAdvancer(
            new TestOptionsSnapshot<CombatOptions>(
                new CombatOptions { MaxResistancePercent = 0.75f }
            ),
            EffectTimeScale.Unscaled
        );

        // Act
        var result = advancer.Advance(state, TestTime.AfterRounds(1));

        // Assert
        Assert.Equal(
            expectedDamage,
            Assert.IsType<DamageEffectTick>(Assert.Single(result.Ticks)).Damage
        );
        Assert.Equal(1000 - expectedDamage, state.CurrentHp);
    }

    [Fact]
    public void Advance_DoesNotRepeatTicks_WhenTheSameInstantIsReconciledAgain()
    {
        // Arrange
        var state = MakeState();
        state.ActiveDots.Add(MakeDot(amount: 20, expiresAfterRounds: 3));
        _advancer.Advance(state, TestTime.AfterRounds(1));

        // Act
        var result = _advancer.Advance(state, TestTime.AfterRounds(1));

        // Assert
        Assert.False(result.Changed);
        Assert.Empty(result.Ticks);
        Assert.Equal(980, state.CurrentHp);
    }

    [Fact]
    public void Advance_RemovesExpiredEffects_AndKeepsTheRest()
    {
        // Arrange
        var state = MakeState();
        state.ActiveBuffs.Add(MakeBuff("Expired", expiresAfterRounds: 2));
        state.ActiveBuffs.Add(MakeBuff("Lasting", expiresAfterRounds: 5));
        state.ActiveConditions[ConditionType.Stunned] = TestTime.AfterRounds(2);
        state.ActiveConditions[ConditionType.Blinded] = TestTime.AfterRounds(5);
        state.CooldownReadyAtByAbility["Smite"] = TestTime.AfterRounds(2);
        state.CooldownReadyAtByAbility["Cleave"] = TestTime.AfterRounds(5);

        // Act
        _advancer.Advance(state, TestTime.AfterRounds(2));

        // Assert
        Assert.Equal("Lasting", Assert.Single(state.ActiveBuffs).AbilityName);
        Assert.Equal(ConditionType.Blinded, Assert.Single(state.ActiveConditions).Key);
        Assert.Equal("Cleave", Assert.Single(state.CooldownReadyAtByAbility).Key);
    }
}
