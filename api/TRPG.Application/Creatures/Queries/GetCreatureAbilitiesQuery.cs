using Microsoft.EntityFrameworkCore;
using TRPG.Application.Abilities;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureAbilitiesQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureAbilitiesQueryHandler(TrpgDbContext context)
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
