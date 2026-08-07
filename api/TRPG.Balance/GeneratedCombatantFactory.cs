using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Balance;

public record GeneratedCombatantContext(CreatureGenerator Generator);

public static class GeneratedCombatantFactory
{
    public static GeneratedCombatantContext CreateContext(CreatureGeneratorOptions? options = null)
    {
        var itemGenerator = new ItemGenerator(
            new WeaponGenerator(),
            new ArmorGenerator(),
            new AccessoryGenerator(),
            new ConsumableGenerator(),
            new AmmoGenerator()
        );
        var generatorOptions = options ?? new CreatureGeneratorOptions();
        var generator = new CreatureGenerator(
            itemGenerator,
            new FixedOptionsSnapshot<CreatureGeneratorOptions>(generatorOptions),
            new StatFormulas(new FixedOptionsSnapshot<CreatureGeneratorOptions>(generatorOptions))
        );

        return new GeneratedCombatantContext(generator);
    }

    public static Combatant ToCombatant(
        CreatureGeneratorResult result,
        bool isPlayer,
        CombatOptions? combatOptions = null
    )
    {
        var abilities = result
            .Abilities.Select(a =>
                AbilityCatalog.Abilities.FirstOrDefault(x => x.Name == a.AbilityName)
            )
            .OfType<Ability>()
            .Prepend(AbilityCatalog.Strike)
            .Distinct()
            .ToArray();

        var weaponProficiencies = isPlayer
            ? Enum.GetValues<WeaponType>()
                .ToDictionary(type => type, _ => result.Creature.Level * 10)
            : new Dictionary<WeaponType, int>();

        return Combatant.FromCreature(
            combatOptions ?? new CombatOptions(),
            isPlayer,
            result.Creature,
            abilities,
            result.Items,
            weaponProficiencies
        );
    }
}
