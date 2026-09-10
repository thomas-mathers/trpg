using Microsoft.EntityFrameworkCore;
using TRPG.Application.Abilities;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureAbilitiesQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureAbilitiesQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetCreatureAbilitiesQuery, IReadOnlyList<Ability>>
{
    public async Task<IReadOnlyList<Ability>> Handle(
        GetCreatureAbilitiesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var skillExperience = await context
            .CreatureSkills.AsNoTracking()
            .Where(skill =>
                skill.CreatureId == query.CreatureId && (skill.Level > 0 || skill.Experience > 0)
            )
            .ToDictionaryAsync(skill => skill.Skill, skill => skill.Experience, cancellationToken);

        return AbilityCatalog.GetAbilitiesForSkillExperience(skillExperience);
    }
}
