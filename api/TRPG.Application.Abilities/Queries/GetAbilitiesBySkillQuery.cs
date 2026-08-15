using TRPG.Application.Common.Handling;
using TRPG.Data.Models;

namespace TRPG.Application.Abilities.Queries;

public class GetAbilitiesBySkillQuery
{
    public required Skill Skill { get; init; }
}

public class GetAbilitiesBySkillQueryHandler
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
