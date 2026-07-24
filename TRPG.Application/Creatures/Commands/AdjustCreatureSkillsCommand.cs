using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot
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

        var totalSkillLevelsGained = 0;

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
                totalSkillLevelsGained++;
            }
        }

        if (totalSkillLevelsGained > 0)
        {
            var creature = await context.Creatures.FirstAsync(
                c => c.Id == command.CreatureId,
                cancellationToken
            );

            var skillLevels = skills.Select(s => s.Level).ToArray();

            creature.Level = SkillFormulas.CalculateLevelFromSkillLevels(skillLevels);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
