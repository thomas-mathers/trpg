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

        var skillRows = await context
            .CreatureSkills.Where(s =>
                s.WorldId == command.WorldId
                && s.CreatureId == command.CreatureId
                && command.UsageCounts.Keys.Contains(s.Skill)
            )
            .ToDictionaryAsync(s => s.Skill, cancellationToken);

        var totalLevelsGained = 0;

        foreach (var (skill, useCount) in command.UsageCounts)
        {
            if (!skillRows.TryGetValue(skill, out var row))
            {
                continue;
            }

            row.Experience += useCount * optionsSnapshot.Value.SkillExperiencePerAbilityUse;

            while (row.Experience >= SkillFormulas.XpForSkillLevel(row.Level + 1))
            {
                row.Level++;
                totalLevelsGained++;
            }
        }

        if (totalLevelsGained > 0)
        {
            var creature = await context.Creatures.FirstAsync(
                c => c.Id == command.CreatureId,
                cancellationToken
            );

            creature.Experience +=
                totalLevelsGained * optionsSnapshot.Value.CharacterExperiencePerSkillLevel;

            while (creature.Experience >= SkillFormulas.XpForCharacterLevel(creature.Level + 1))
            {
                creature.Level++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
