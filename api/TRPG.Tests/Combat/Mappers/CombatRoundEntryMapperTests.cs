using TRPG.Application.Combat.Events;
using TRPG.Combat.ClientModels;
using TRPG.Combat.Mappers;
using DomainConditionType = TRPG.Application.Abilities.ConditionType;
using DomainDamageType = TRPG.Domain.Models.DamageType;

namespace TRPG.Tests.Combat.Mappers;

public class CombatRoundEntryMapperTests
{
    [Fact]
    public void ToCombatRoundEntries_MapsHit_ToCombatHitEntry()
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
        var events = CombatRoundEntryMapper.ToCombatRoundEntries([hit]);

        // Assert
        var mapped = Assert.IsType<CombatHitEntry>(Assert.Single(events));
        Assert.Equal(attackerId, mapped.AttackerId);
        Assert.Equal(targetId, mapped.TargetId);
        Assert.Equal(15, mapped.Damage);
        Assert.True(mapped.IsCritical);
        Assert.Equal(DamageType.Fire, mapped.DamageType);
        Assert.Equal([ConditionType.Burning], mapped.AppliedConditions);
    }

    [Fact]
    public void ToCombatRoundEntries_MapsMiss_ToCombatMissEntry()
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
        var events = CombatRoundEntryMapper.ToCombatRoundEntries([miss]);

        // Assert
        Assert.IsType<CombatMissEntry>(Assert.Single(events));
    }

    [Fact]
    public void ToCombatRoundEntries_MapsBlock_ToCombatBlockEntry()
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
        var events = CombatRoundEntryMapper.ToCombatRoundEntries([block]);

        // Assert
        Assert.IsType<CombatBlockEntry>(Assert.Single(events));
    }

    [Fact]
    public void ToCombatRoundEntries_SkipsResolutionsWithoutUiRepresentations()
    {
        // Arrange
        var noAction = new NoAction("Hero", DomainConditionType.Stunned);

        // Act
        var events = CombatRoundEntryMapper.ToCombatRoundEntries([noAction]);

        // Assert
        Assert.Empty(events);
    }
}
