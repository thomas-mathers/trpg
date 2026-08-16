using TRPG.Application.Common.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Abilities.Queries;

public class GetAbilitiesBySkillQuery
{
    public required Skill Skill { get; init; }
}

internal class GetAbilitiesBySkillQueryHandler
    : IQueryHandler<GetAbilitiesBySkillQuery, IReadOnlyCollection<Ability>>
{
    public Task<IReadOnlyCollection<Ability>> Handle(
        GetAbilitiesBySkillQuery query,
        CancellationToken cancellationToken = default
    ) =>
        Task.FromResult<IReadOnlyCollection<Ability>>(
            AbilityCatalog.Abilities.Where(a => a.Skill == query.Skill).ToArray()
        );
}
