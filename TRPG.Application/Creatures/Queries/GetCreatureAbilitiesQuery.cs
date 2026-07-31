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
        var abilityNames = await context
            .CreatureAbilities.AsNoTracking()
            .Where(a => a.CreatureId == query.CreatureId)
            .Select(a => a.AbilityName)
            .ToArrayAsync(cancellationToken);

        var learnedAbilities = abilityNames.Select(name =>
            AbilityCatalog.Abilities.FirstOrDefault(a => a.Name == name)
        );

        return [AbilityCatalog.Strike, .. learnedAbilities.OfType<Ability>()];
    }
}
