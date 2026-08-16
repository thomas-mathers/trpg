using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class EnemyCombatActionResolverTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    private static readonly IOptionsSnapshot<CombatOptions> DefaultOptions =
        new TestOptionsSnapshot<CombatOptions>(new CombatOptions());

    private static readonly EnemyCombatActionResolver Resolver = new(
        DefaultOptions,
        new DamageCalculator(DefaultOptions),
        new HitCalculator(DefaultOptions)
    );
    private static readonly SupportAbility BlockStance = AbilityCatalog.Block;

    private static EnemyCombatActionResolver MakeResolver(float openingBuffChancePercent)
    {
        var options = new TestOptionsSnapshot<CombatOptions>(
            new CombatOptions { OpeningBuffChancePercent = openingBuffChancePercent }
        );
        return new EnemyCombatActionResolver(
            options,
            new DamageCalculator(options),
            new HitCalculator(options)
        );
    }

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
        Assert.IsType<SupportAbility>(abilityAction.Ability);
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
        var monster = MakeCombatant("Wraith")
            .WithAbilities(BlockStance)
            .WithItem(apPotion)
            .WithCurrentHp(1)
            .Build();

        // Act
        var result = Resolver.Resolve(monster, player);

        // Assert — raises its guard as a last resort rather than drinking an unrelated potion
        var abilityAction = Assert.IsType<ResolvedUseAbilityAction>(result);
        Assert.Equal("Block", abilityAction.Ability.Name);
    }

    [Fact]
    public void Resolve_CastsOpeningBuff_WhenChanceIsAlwaysTaken()
    {
        // Arrange — a long-running buff (Duration > 1) is eligible; OpeningBuffChancePercent: 1
        // makes the coin flip deterministic instead of needing statistical trials
        var battleStance = Builders.MakeBuffAbility("Battle Stance");
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(battleStance).Build();

        // Act
        var result = MakeResolver(openingBuffChancePercent: 1f).Resolve(monster, player);

        // Assert
        Assert.Equal("Battle Stance", Assert.IsType<ResolvedUseAbilityAction>(result).Ability.Name);
    }

    [Fact]
    public void Resolve_PicksHighestValueBuff_WhenMultipleAreEligible()
    {
        // Arrange — a Strength buff boosts the monster's physical Strike; a MovementSpeed-only
        // buff provides no offensive or defensive value, so it should never win the comparison
        var strengthBuff = Builders.MakeBuffAbility(
            "Battle Stance",
            attribute: AttributeName.Strength,
            amount: 50
        );
        var speedBuff = Builders.MakeBuffAbility(
            "Haste",
            attribute: AttributeName.MovementSpeed,
            amount: 10
        );

        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(strengthBuff, speedBuff).Build();

        // Act
        var result = MakeResolver(openingBuffChancePercent: 1f).Resolve(monster, player);

        // Assert
        Assert.Equal("Battle Stance", Assert.IsType<ResolvedUseAbilityAction>(result).Ability.Name);
    }

    [Fact]
    public void Resolve_AttacksInstead_WhenOpeningBuffChanceIsNeverTaken()
    {
        // Arrange — same eligible buff as above, but OpeningBuffChancePercent: 0 means the coin
        // flip never favors it, so the monster falls through to attacking
        var battleStance = Builders.MakeBuffAbility("Battle Stance");
        var player = MakeCombatant("Hero").AsPlayer().Build();
        var monster = MakeCombatant("Wraith").WithAbilities(battleStance).Build();

        // Act
        var result = MakeResolver(openingBuffChancePercent: 0f).Resolve(monster, player);

        // Assert
        Assert.Equal("Strike", Assert.IsType<ResolvedUseAbilityAction>(result).Ability.Name);
    }
}
