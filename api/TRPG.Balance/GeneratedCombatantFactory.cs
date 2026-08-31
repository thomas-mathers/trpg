using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

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
            new FixedOptionsSnapshot<CreatureGeneratorOptions>(generatorOptions)
        );

        return new GeneratedCombatantContext(generator);
    }

    internal static Combatant ToCombatant(
        CreatureGeneratorResult result,
        bool isPlayer,
        CombatOptions? combatOptions = null
    )
    {
        var skillLevels = result.Skills.ToDictionary(skill => skill.Skill, skill => skill.Level);
        var abilities = AbilityCatalog.GetAbilitiesForSkillLevels(skillLevels);

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
