using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal record CreatureSkillProgress(
    Skill Skill,
    int Level,
    int ExperienceCurrent,
    int ExperienceToNextLevel
);

internal class GetCreatureSkillsQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureSkillsQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<CreatureSkillProgress>> Handle(
        GetCreatureSkillsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var skills = await context
            .CreatureSkills.AsNoTracking()
            .Where(s => s.CreatureId == query.CreatureId)
            .ToArrayAsync(cancellationToken);

        return skills
            .Select(s =>
            {
                var levelFloor = SkillFormulas.XpForSkillLevel(s.Level);
                var nextLevelXp = SkillFormulas.XpForSkillLevel(s.Level + 1);
                return new CreatureSkillProgress(
                    s.Skill,
                    s.Level,
                    s.Experience - levelFloor,
                    nextLevelXp - levelFloor
                );
            })
            .ToArray();
    }
}
