using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Application.Balance;

public record GeneratedCombatantContext(
    CreatureGenerator Generator,
    AbilityDefinitions AbilityDefinitions
);

// Builds combatants through the real CreatureGenerator (procedural attributes, skills,
// abilities, and archetype-driven starting gear) rather than SimulatedCombatantFactory's
// directly-specified CombatantSpec - used for experiments that need a realistic player
// (generated per Profession) or monster (generated per CreatureType/CreatureArchetype).
public static class GeneratedCombatantFactory
{
    public static GeneratedCombatantContext CreateContext(CreatureGeneratorOptions? options = null)
    {
        var abilityDefinitions = AbilityDefinitions.Create();
        var itemGenerator = new ItemGenerator(
            new WeaponGenerator(abilityDefinitions),
            new ArmorGenerator(abilityDefinitions),
            new AccessoryGenerator(),
            new ConsumableGenerator(),
            new AmmoGenerator()
        );
        var generatorOptions = options ?? new CreatureGeneratorOptions();
        var generator = new CreatureGenerator(
            itemGenerator,
            abilityDefinitions,
            new FixedOptionsSnapshot<CreatureGeneratorOptions>(generatorOptions),
            new StatFormulas(new FixedOptionsSnapshot<CreatureGeneratorOptions>(generatorOptions))
        );

        return new GeneratedCombatantContext(generator, abilityDefinitions);
    }

    public static Combatant ToCombatant(
        CreatureGeneratorResult result,
        AbilityDefinitions abilityDefinitions,
        bool isPlayer
    )
    {
        var abilities = result
            .Abilities.Select(a => abilityDefinitions.GetByName(a.AbilityName))
            .OfType<Ability>()
            .Prepend(abilityDefinitions.BasicAttack)
            .Distinct()
            .ToArray();

        // Non-player hit chance is purely formulaic now (see HitCalculator.CalculateHitChance,
        // CombatOptions.NonPlayerProficiencyBase/PerLevel) - this dictionary only matters for the
        // player, who accumulates real proficiency through actual play instead (one point per
        // physical swing - see AdjustWeaponProficienciesCommandHandler). A freshly-generated
        // simulated player would otherwise always start at 0, understating how a genuinely-played
        // character of that level would actually hit - approximated here as level x 10.
        var weaponProficiencies = isPlayer
            ? Enum.GetValues<WeaponType>()
                .ToDictionary(type => type, _ => result.Creature.Level * 10)
            : new Dictionary<WeaponType, int>();

        return Combatant.FromCreature(
            result.Creature,
            abilities,
            isPlayer,
            result.Items,
            weaponProficiencies
        );
    }
}
