using TRPG.Application.Combat.Events;
using TRPG.Combat.ClientModels;
using TRPG.Combat.Mappers;
using DomainConditionType = TRPG.Application.Abilities.ConditionType;
using DomainDamageType = TRPG.Domain.Models.DamageType;

namespace TRPG.Tests.Combat.Mappers;

public class CombatActionResultMapperTests
{
    [Fact]
    public void ToCombatActionResults_MapsHit()
    {
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

        IReadOnlyList<CombatResolution> resolutions = [hit];

        var result = Assert.Single(resolutions.ToCombatActionResults());

        Assert.Equal(CombatActionOutcome.Hit, result.Outcome);
        Assert.Equal(attackerId, result.AttackerId);
        Assert.Equal(targetId, result.TargetId);
        Assert.Equal(15, result.Damage);
        Assert.True(result.IsCritical);
        Assert.Equal(DamageType.Fire, result.DamageType);
        Assert.Equal([ConditionType.Burning], result.AppliedConditions);
    }

    [Fact]
    public void ToCombatActionResults_MapsMiss()
    {
        var miss = new Miss(
            AttackerId: Guid.NewGuid(),
            AttackerName: "Hero",
            AbilityName: "Slash",
            TargetId: Guid.NewGuid(),
            TargetName: "Wraith"
        );

        IReadOnlyList<CombatResolution> resolutions = [miss];

        var result = Assert.Single(resolutions.ToCombatActionResults());

        Assert.Equal(CombatActionOutcome.Miss, result.Outcome);
        Assert.Null(result.Damage);
    }

    [Fact]
    public void ToCombatActionResults_SkipsResolutionsWithoutPlayerFeedback()
    {
        var noAction = new NoAction("Hero", DomainConditionType.Stunned);

        IReadOnlyList<CombatResolution> resolutions = [noAction];

        var results = resolutions.ToCombatActionResults();

        Assert.Empty(results);
    }
}
