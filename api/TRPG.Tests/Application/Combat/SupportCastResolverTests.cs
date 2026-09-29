using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class SupportCastResolverTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    private Combatant MakePlayer(params IReadOnlyList<Ability> abilities) =>
        Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithName("Hero")
            .AsPlayer()
            .WithAbilities(abilities)
            .Build();

    private Combatant MakeOther(int currentHp = 10) =>
        Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithName("Bystander")
            .WithCurrentHp(currentHp)
            .Build();

    private static ResolvedUseAbilityAction ResolveSuccessfully(
        Combatant player,
        Combatant other,
        Guid targetId,
        string abilityName
    )
    {
        var resolved = SupportCastResolver.Resolve(
            [player, other],
            new UseAbilityAction(targetId, abilityName),
            TestTime.Start
        );
        return Assert.IsType<ResolvedUseAbilityAction>(resolved.Result);
    }

    [Fact]
    public void Resolve_TargetsTheClickedCreature_ForSingleTargetSupport()
    {
        // Arrange
        var cure = Builders.MakeHealSupportAbility("Cure");
        var player = MakePlayer(cure);
        var other = MakeOther();

        // Act
        var resolved = ResolveSuccessfully(player, other, other.CreatureId, "Cure");

        // Assert
        Assert.Same(other, Assert.Single(resolved.Targets));
    }

    [Fact]
    public void Resolve_TargetsTheCaster_ForSelfSupport()
    {
        // Arrange
        var stance = Builders.MakeBuffSupportAbility("Stance", targetType: TargetType.Self);
        var player = MakePlayer(stance);
        var other = MakeOther();

        // Act
        var resolved = ResolveSuccessfully(player, other, other.CreatureId, "Stance");

        // Assert
        Assert.Same(player, Assert.Single(resolved.Targets));
    }

    [Fact]
    public void Resolve_TargetsTheAllies_ForAoeSupport()
    {
        // Arrange
        var rally = Builders.MakeBuffSupportAbility("Rally", targetType: TargetType.Aoe);
        var player = MakePlayer(rally);
        var other = MakeOther();

        // Act
        var resolved = ResolveSuccessfully(player, other, other.CreatureId, "Rally");

        // Assert
        Assert.Same(player, Assert.Single(resolved.Targets));
    }

    [Fact]
    public void Resolve_Fails_ForAnAttackAbility()
    {
        // Arrange
        var player = MakePlayer(AbilityCatalog.Strike);
        var other = MakeOther();

        // Act
        var resolved = SupportCastResolver.Resolve(
            [player, other],
            new UseAbilityAction(other.CreatureId, AbilityCatalog.Strike.Name),
            TestTime.Start
        );

        // Assert
        Assert.True(resolved.IsError);
    }

    [Fact]
    public void Resolve_Fails_WhenTheTargetIsDead()
    {
        // Arrange
        var player = MakePlayer(Builders.MakeHealSupportAbility("Cure"));
        var other = MakeOther(currentHp: 0);

        // Act
        var resolved = SupportCastResolver.Resolve(
            [player, other],
            new UseAbilityAction(other.CreatureId, "Cure"),
            TestTime.Start
        );

        // Assert
        Assert.True(resolved.IsError);
    }

    [Fact]
    public void Resolve_Fails_WhenThePlayerCannotAffordTheCost()
    {
        // Arrange
        var player = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .AsPlayer()
            .WithAbilities(Builders.MakeHealSupportAbility("Cure", cost: 5))
            .WithCurrentAp(1)
            .Build();
        var other = MakeOther();

        // Act
        var resolved = SupportCastResolver.Resolve(
            [player, other],
            new UseAbilityAction(other.CreatureId, "Cure"),
            TestTime.Start
        );

        // Assert
        Assert.Contains("costs 5 AP", resolved.ErrorMessage, StringComparison.Ordinal);
    }
}
