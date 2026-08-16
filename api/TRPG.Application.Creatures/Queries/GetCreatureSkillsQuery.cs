using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Application.CreatureFormulas;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public record CreatureSkillProgress(
    Skill Skill,
    int Level,
    int ExperienceCurrent,
    int ExperienceToNextLevel
);

public class GetCreatureSkillsQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureSkillsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCreatureSkillsQuery, IReadOnlyCollection<CreatureSkillProgress>>
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
                var levelFloor = SkillFormulas.CalculateSkillExperienceFromSkillLevel(s.Level);
                var nextLevelXp = SkillFormulas.CalculateSkillExperienceFromSkillLevel(s.Level + 1);
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
