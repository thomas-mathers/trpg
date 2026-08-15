using Microsoft.EntityFrameworkCore;
using TRPG.Application.Abilities;
using TRPG.Application.Common.Handling;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureAbilitiesQuery
{
    public required Guid CreatureId { get; init; }
}

public class GetCreatureAbilitiesQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCreatureAbilitiesQuery, IReadOnlyList<Ability>>
{
    public async Task<IReadOnlyList<Ability>> Handle(
        GetCreatureAbilitiesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var skillLevels = await context
            .CreatureSkills.AsNoTracking()
            .Where(skill => skill.CreatureId == query.CreatureId)
            .ToDictionaryAsync(skill => skill.Skill, skill => skill.Level, cancellationToken);

        return AbilityCatalog.GetAbilitiesForSkillLevels(skillLevels);
    }
}
