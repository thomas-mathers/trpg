using TRPG.Application.Combat;
using TRPG.Application.Combat.ClientEvents;
using TRPG.Application.Combat.Events;
using TRPG.Application.Combat.Mappers;
using DomainConditionType = TRPG.Application.Abilities.ConditionType;
using DomainDamageType = TRPG.Domain.Models.DamageType;

namespace TRPG.Tests.Application.Combat.Mappers;

public class CombatRoundEventMapperTests
{
    [Fact]
    public void ToCombatRoundEvents_MapsHit_ToCombatHitEvent()
    {
        // Arrange
        var attackerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var hit = new Hit(
            AttackerId: attackerId,
            AttackerName: "Hero",
            AbilityName: "Slash",
            TargetId: targetId,
            TargetName: "Wraith",
            TargetRemainingHp: 5,
            TargetMaximumHp: 20,
            Killed: false,
            IsCritical: true,
            Damage: 15,
            DamageType: DomainDamageType.Fire,
            AppliedConditions: [DomainConditionType.Burning]
        );

        // Act
        var events = CombatRoundEventMapper.ToCombatRoundEvents([hit]);

        // Assert
        var mapped = Assert.IsType<CombatHitEvent>(Assert.Single(events));
        Assert.Equal(attackerId, mapped.AttackerId);
        Assert.Equal(targetId, mapped.TargetId);
        Assert.Equal(15, mapped.Damage);
        Assert.True(mapped.IsCritical);
        Assert.Equal(DamageType.Fire, mapped.DamageType);
        Assert.Equal([ConditionType.Burning], mapped.AppliedConditions);
    }

    [Fact]
    public void ToCombatRoundEvents_MapsMiss_ToCombatMissEvent()
    {
        // Arrange
        var miss = new Miss(
            AttackerId: Guid.NewGuid(),
            AttackerName: "Hero",
            AbilityName: "Slash",
            TargetId: Guid.NewGuid(),
            TargetName: "Wraith"
        );

        // Act
        var events = CombatRoundEventMapper.ToCombatRoundEvents([miss]);

        // Assert
        Assert.IsType<CombatMissEvent>(Assert.Single(events));
    }

    [Fact]
    public void ToCombatRoundEvents_MapsBlock_ToCombatBlockEvent()
    {
        // Arrange
        var block = new Block(
            AttackerId: Guid.NewGuid(),
            AttackerName: "Hero",
            AbilityName: "Slash",
            TargetId: Guid.NewGuid(),
            TargetName: "Wraith"
        );

        // Act
        var events = CombatRoundEventMapper.ToCombatRoundEvents([block]);

        // Assert
        Assert.IsType<CombatBlockEvent>(Assert.Single(events));
    }

    [Fact]
    public void ToCombatRoundEvents_SkipsEventTypesWithoutAUiRepresentation()
    {
        // Arrange
        var noAction = new NoAction("Hero", DomainConditionType.Stunned);

        // Act
        var events = CombatRoundEventMapper.ToCombatRoundEvents([noAction]);

        // Assert
        Assert.Empty(events);
    }
}
