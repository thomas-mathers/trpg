using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Events;
using TRPG.Application.Configuration;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Commands;

internal class AdjustCreatureSkillsCommand
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
    public required IReadOnlyDictionary<Skill, int> UsageCounts { get; init; }
}

internal class AdjustCreatureSkillsCommandHandler(
    TrpgDbContext context,
    IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot,
    IGameClientEventSink gameEvents
)
{
    public async Task Handle(
        AdjustCreatureSkillsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.UsageCounts.Count == 0)
        {
            return;
        }

        var skills = await context
            .CreatureSkills.Where(s =>
                s.WorldId == command.WorldId && s.CreatureId == command.CreatureId
            )
            .ToListAsync(cancellationToken);

        var skillLevelUps = new List<(Skill Skill, int Level)>();

        foreach (var skill in skills)
        {
            var usageCount = command.UsageCounts.GetValueOrDefault(skill.Skill, 0);

            skill.Experience += usageCount * optionsSnapshot.Value.SkillExperiencePerAbilityUse;

            while (
                skill.Experience
                >= SkillFormulas.CalculateSkillExperienceFromSkillLevel(skill.Level + 1)
            )
            {
                skill.Level++;
                skillLevelUps.Add((skill.Skill, skill.Level));
            }
        }

        var characterLevelUps = new List<CharacterLevelUpEvent>();
        if (skillLevelUps.Count > 0)
        {
            var creature = await context.Creatures.FirstAsync(
                c => c.Id == command.CreatureId,
                cancellationToken
            );

            var skillLevels = skills.Select(s => s.Level).ToArray();

            var previousLevel = creature.Level;
            creature.Level = SkillFormulas.CalculateLevelFromSkillLevels(skillLevels);
            for (var level = previousLevel + 1; level <= creature.Level; level++)
            {
                characterLevelUps.Add(new CharacterLevelUpEvent(level));
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var totalCharacterExperience = skills.Sum(s =>
            SkillFormulas.CalculateExperienceFromSkillLevel(s.Level)
        );
        var characterExperience = SkillFormulas.GetExperienceProgress(
            characterLevelUps.LastOrDefault()?.Level
                ?? (
                    skillLevelUps.Count > 0
                        ? SkillFormulas.CalculateLevelFromSkillLevels(
                            skills.Select(s => s.Level).ToArray()
                        )
                        : 1
                ),
            totalCharacterExperience
        );

        foreach (var (skill, level) in skillLevelUps)
        {
            gameEvents.Enqueue(
                new SkillLevelUpEvent(
                    skill,
                    level,
                    characterExperience.Current,
                    characterExperience.ToNextLevel
                )
            );
        }
        foreach (var characterLevelUp in characterLevelUps)
        {
            gameEvents.Enqueue(characterLevelUp);
        }
    }
}
