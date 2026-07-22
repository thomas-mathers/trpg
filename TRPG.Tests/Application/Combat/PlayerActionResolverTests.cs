using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class PlayerActionResolverTests
{
    private static readonly AttackAbility BasicAttack = AbilityDefinitions.Create().BasicAttack;
    private static readonly BuffAbility BlockStance = AbilityDefinitions.Create().BlockStance;
    private readonly Guid _worldId = Guid.NewGuid();

    private Combatant MakeCombatant(
        string name,
        bool isPlayer = false,
        IReadOnlyList<Ability>? abilities = null,
        IReadOnlyList<UsableItem>? usableItems = null
    )
    {
        var creature = Builders.MakeCreature(_worldId, name: name);
        return Combatant.FromCreature(
            creature,
            abilities ?? [],
            BasicAttack,
            BlockStance,
            isPlayer,
            [],
            new Dictionary<WeaponType, int>(),
            usableItems ?? []
        );
    }

    [Fact]
    public void Resolve_ResolvesAsAbility_WhenActionNameMatchesAKnownAbility()
    {
        // Arrange
        var smite = new AttackAbility
        {
            Name = "Smite",
            Description = "A test attack.",
            TargetType = AttackTargetType.Single,
            DamageType = DamageType.Physical,
            DamageAmount = 5,
            DamageAmountType = AmountType.Flat,
        };
        var player = MakeCombatant("Hero", isPlayer: true, abilities: [smite]);
        var monster = MakeCombatant("Wraith");
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act
        var resolution = PlayerActionResolver.Resolve(combatants, "Smite", "Wraith");

        // Assert
        var resolved = Assert.IsType<ActionResolved>(resolution);
        var resolvedAbility = Assert.IsType<ResolvedAbility>(resolved.Action);
        Assert.Equal("Smite", resolvedAbility.Ability.Name);
    }

    [Fact]
    public void Resolve_ResolvesAsItem_WhenActionNameDoesNotMatchAnyAbility()
    {
        // Arrange
        var potion = new UsableItem(Guid.NewGuid(), "Health Potion", ResourceType.Hp, 20);
        var player = MakeCombatant("Hero", isPlayer: true, usableItems: [potion]);
        IReadOnlyList<Combatant> combatants = [player];

        // Act
        var resolution = PlayerActionResolver.Resolve(combatants, "Health Potion", "Hero");

        // Assert
        var resolved = Assert.IsType<ActionResolved>(resolution);
        var resolvedItem = Assert.IsType<ResolvedItem>(resolved.Action);
        Assert.Equal("Health Potion", resolvedItem.Item.Name);
    }

    [Fact]
    public void Resolve_ReturnsResolvedItem_WhenTheNamedItemExists()
    {
        // Arrange
        var potion = new UsableItem(Guid.NewGuid(), "Health Potion", ResourceType.Hp, 20);
        var player = MakeCombatant("Hero", isPlayer: true, usableItems: [potion]);
        IReadOnlyList<Combatant> combatants = [player];

        // Act
        var resolution = PlayerActionResolver.Resolve(combatants, new UseItem("Health Potion"));

        // Assert
        var resolved = Assert.IsType<ActionResolved>(resolution);
        var resolvedItem = Assert.IsType<ResolvedItem>(resolved.Action);
        Assert.Equal(potion.ItemId, resolvedItem.Item.ItemId);
    }

    [Fact]
    public void Resolve_ReturnsActionRejected_WhenTheNamedItemDoesNotExist()
    {
        // Arrange
        var player = MakeCombatant("Hero", isPlayer: true);
        IReadOnlyList<Combatant> combatants = [player];

        // Act
        var resolution = PlayerActionResolver.Resolve(combatants, new UseItem("Health Potion"));

        // Assert
        Assert.IsType<ActionRejected>(resolution);
    }
}
