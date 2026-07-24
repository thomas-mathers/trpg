using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class PlayerActionResolverTests
{
    private readonly Guid _worldId = Guid.NewGuid();

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
        var player = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithName("Hero")
            .AsPlayer()
            .WithAbilities(smite)
            .Build();
        var monster = Builders.NewCombatant().WithWorldId(_worldId).WithName("Wraith").Build();
        IReadOnlyList<Combatant> combatants = [player, monster];

        // Act
        var resolution = PlayerActionResolver.Resolve(combatants, monster.CreatureId, "Smite");

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
        var player = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithName("Hero")
            .AsPlayer()
            .WithUsableItems(potion)
            .Build();
        IReadOnlyList<Combatant> combatants = [player];

        // Act
        var resolution = PlayerActionResolver.Resolve(
            combatants,
            player.CreatureId,
            "Health Potion"
        );

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
        var player = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithName("Hero")
            .AsPlayer()
            .WithUsableItems(potion)
            .Build();
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
        var player = Builders
            .NewCombatant()
            .WithWorldId(_worldId)
            .WithName("Hero")
            .AsPlayer()
            .Build();
        IReadOnlyList<Combatant> combatants = [player];

        // Act
        var resolution = PlayerActionResolver.Resolve(combatants, new UseItem("Health Potion"));

        // Assert
        Assert.IsType<ActionRejected>(resolution);
    }
}
