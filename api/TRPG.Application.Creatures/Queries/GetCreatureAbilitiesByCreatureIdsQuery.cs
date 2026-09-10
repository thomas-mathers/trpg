using Microsoft.EntityFrameworkCore;
using TRPG.Application.Abilities;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureAbilitiesByCreatureIdsQuery
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class GetCreatureAbilitiesByCreatureIdsQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<
        GetCreatureAbilitiesByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Ability>>
    >
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Ability>>> Handle(
        GetCreatureAbilitiesByCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var skills = await context
            .CreatureSkills.AsNoTracking()
            .Where(skill =>
                query.CreatureIds.AsEnumerable().Contains(skill.CreatureId)
                && (skill.Level > 0 || skill.Experience > 0)
            )
            .ToArrayAsync(cancellationToken);

        var skillExperienceByCreature = skills
            .GroupBy(skill => skill.CreatureId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(skill => skill.Skill, skill => skill.Experience)
            );

        return query.CreatureIds.ToDictionary(
            creatureId => creatureId,
            creatureId =>
                AbilityCatalog.GetAbilitiesForSkillExperience(
                    skillExperienceByCreature.GetValueOrDefault(
                        creatureId,
                        new Dictionary<Skill, int>()
                    )
                )
        );
    }
}
