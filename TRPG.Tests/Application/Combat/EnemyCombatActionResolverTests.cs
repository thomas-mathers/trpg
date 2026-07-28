using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class EnemyCombatActionResolverTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    private static readonly IOptionsSnapshot<CombatOptions> DefaultOptions =
        new TestOptionsSnapshot<CombatOptions>(new CombatOptions());

    private static readonly EnemyCombatActionResolver Resolver = new(DefaultOptions);

    private CombatantBuilder MakeCombatant(string name) =>
        Builders.NewCombatant().WithWorldId(_worldId).WithName(name);

    [Fact]
    public void Resolve_DrinksHealthPotion_WhenLowOnHpAndNoHealAbility()
    {
        // Arrange
        var potion = new Consumable
        {
            Name = "Health Potion",
            Resource = ResourceType.Hp,
            RestoreAmount = 50,
        };
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithItem(potion).WithCurrentHp(1).Build();

        // Act
        var result = Resolver.Resolve(monster, player);

        // Assert
        var itemAction = Assert.IsType<ResolvedUseItemAction>(result);
        Assert.Equal(ResourceType.Hp, itemAction.Item.Resource);
    }

    [Fact]
    public void Resolve_UsesHealAbility_OverHealthPotion_WhenBothAreAvailable()
    {
        // Arrange
        var potion = new Consumable
        {
            Name = "Health Potion",
            Resource = ResourceType.Hp,
            RestoreAmount = 50,
        };
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith")
            .WithAbilities(Builders.MakeInstantHealAbility())
            .WithItem(potion)
            .WithCurrentHp(1)
            .Build();

        // Act
        var result = Resolver.Resolve(monster, player);

        // Assert — the ability is preferred; the potion is left untouched
        var abilityAction = Assert.IsType<ResolvedUseAbilityAction>(result);
        Assert.IsType<InstantHealAbility>(abilityAction.Ability);
    }

    [Fact]
    public void Resolve_DrinksApPotion_WhenHpIsFineButApIsLow()
    {
        // Arrange
        var potion = new Consumable
        {
            Name = "Ap Tonic",
            Resource = ResourceType.Ap,
            RestoreAmount = 50,
        };
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithItem(potion).WithCurrentAp(1).Build();

        // Act
        var result = Resolver.Resolve(monster, player);

        // Assert
        var itemAction = Assert.IsType<ResolvedUseItemAction>(result);
        Assert.Equal(ResourceType.Ap, itemAction.Item.Resource);
    }

    [Fact]
    public void Resolve_DrinksMpPotion_WhenHpIsFineButMpIsLow()
    {
        // Arrange
        var potion = new Consumable
        {
            Name = "Mp Tonic",
            Resource = ResourceType.Mp,
            RestoreAmount = 50,
        };
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithItem(potion).WithCurrentMp(1).Build();

        // Act
        var result = Resolver.Resolve(monster, player);

        // Assert
        var itemAction = Assert.IsType<ResolvedUseItemAction>(result);
        Assert.Equal(ResourceType.Mp, itemAction.Item.Resource);
    }

    [Fact]
    public void Resolve_FallsBackToBlock_WhenLowOnHpWithNoHealOrPotionForIt()
    {
        // Arrange — an Ap potion is present but doesn't address the emergency, so it's ignored
        var apPotion = new Consumable
        {
            Name = "Ap Tonic",
            Resource = ResourceType.Ap,
            RestoreAmount = 50,
        };
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithItem(apPotion).WithCurrentHp(1).Build();

        // Act
        var result = Resolver.Resolve(monster, player);

        // Assert — raises its guard as a last resort rather than drinking an unrelated potion
        var abilityAction = Assert.IsType<ResolvedUseAbilityAction>(result);
        Assert.Equal("Block", abilityAction.Ability.Name);
    }

    [Fact]
    public void Resolve_CastsOpeningBuff_WhenHealthyAndNotYetActive()
    {
        // Arrange — a long-running buff (Duration > 1) is cast proactively, unlike Block
        var battleStance = Builders.MakeBuffAbility("Battle Stance");
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(battleStance).Build();

        // Act
        var result = Resolver.Resolve(monster, player);

        // Assert
        var abilityAction = Assert.IsType<ResolvedUseAbilityAction>(result);
        Assert.Equal("Battle Stance", abilityAction.Ability.Name);
    }
}
