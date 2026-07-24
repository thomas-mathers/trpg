using TRPG.Application.Common.Algorithms;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures;

internal static class CreatureSkillsGenerator
{
    private static readonly Skill[] AllSkills = Enum.GetValues<Skill>();

    public static IReadOnlyCollection<CreatureSkill> Generate(
        Creature creature,
        IReadOnlyDictionary<Skill, int> affinityWeights
    )
    {
        var weights = AllSkills.Select(skill => affinityWeights.GetValueOrDefault(skill)).ToArray();
        var skillLevels = DistributeSkillLevels(creature.Level, weights);

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

    private static int[] DistributeSkillLevels(int targetCharacterLevel, int[] weights)
    {
        var skillLevels = AllSkills.Select(_ => 1).ToArray();
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
