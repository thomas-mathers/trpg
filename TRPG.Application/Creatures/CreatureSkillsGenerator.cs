using TRPG.Application.Common.Algorithms;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures;

internal static class CreatureSkillsGenerator
{
    private static readonly Skill[] AllSkills = Enum.GetValues<Skill>();

    public static IReadOnlyCollection<CreatureSkill> Generate(
        Creature creature,
        IReadOnlyDictionary<Skill, int> affinityWeights,
        bool isPlayer
    )
    {
        var weights = AllSkills.Select(skill => affinityWeights.GetValueOrDefault(skill)).ToArray();
        var skillLevels = DistributeSkillLevels(creature.Level, weights, isPlayer);

        return AllSkills
            .Select(
                (skill, i) =>
                    new CreatureSkill
                    {
                        WorldId = creature.WorldId,
                        CreatureId = creature.Id,
                        Skill = skill,
                        Level = skillLevels[i],
                        Experience = SkillFormulas.CalculateSkillExperienceFromSkillLevel(
                            skillLevels[i]
                        ),
                    }
            )
            .ToArray();
    }

    private static int[] DistributeSkillLevels(
        int targetCharacterLevel,
        int[] weights,
        bool isPlayer
    )
    {
        // The player can dabble in anything regardless of class, so every skill starts at 1. A
        // monster only starts at 1 in the skills its archetype actually has affinity for -
        // otherwise it'd end up knowing level-1 abilities (Fireball, Arrow Shot, ...) from trees
        // it has no business in, since RequiredSkillLevel <= 1 would trivially pass for every skill.
        var skillLevels = isPlayer
            ? AllSkills.Select(_ => 1).ToArray()
            : weights.Select(weight => weight > 0 ? 1 : 0).ToArray();
        var currentCharacterLevel = 1;

        while (currentCharacterLevel < targetCharacterLevel)
        {
            var sampledIndex = WeightedSampler.SampleIndex(weights);
            skillLevels[sampledIndex]++;

            var newCharacterLevel = SkillFormulas.CalculateLevelFromSkillLevels(skillLevels);
            if (newCharacterLevel > targetCharacterLevel)
            {
                skillLevels[sampledIndex]--;
                break;
            }

            currentCharacterLevel = newCharacterLevel;
        }

        return skillLevels;
    }
}
