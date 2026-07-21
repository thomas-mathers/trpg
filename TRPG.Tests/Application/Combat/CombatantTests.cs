using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat;

public class CombatantTests
{
    private static readonly AttackAbility BasicAttack = AbilityDefinitions.Create().BasicAttack;
    private static readonly BuffAbility BlockStance = AbilityDefinitions.Create().BlockStance;
    private readonly Guid _worldId = Guid.NewGuid();

    [Fact]
    public void FromCreature_StartsWithNoActiveModifiers_SinceBuffsAreCombatScoped()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId);

        // Act
        var combatant = Combatant.FromCreature(
            creature,
            [],
            BasicAttack,
            BlockStance,
            isPlayer: true,
            [],
            new Dictionary<WeaponType, int>(),
            []
        );

        // Assert
        Assert.Empty(combatant.ActiveBuffs);
    }

    [Fact]
    public void FromCreature_AlwaysIncludesTheBasicAttack_BeforeTheCreaturesOwnAbilities()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId);

        // Act
        var combatant = Combatant.FromCreature(
            creature,
            [],
            BasicAttack,
            BlockStance,
            isPlayer: false,
            [],
            new Dictionary<WeaponType, int>(),
            []
        );

        // Assert
        Assert.Equal(BasicAttack.Name, combatant.Abilities[0].Name);
    }
}
