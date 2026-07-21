using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureAbilitiesQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureAbilitiesQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<string>> Handle(
        GetCreatureAbilitiesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .CreatureAbilities.AsNoTracking()
            .Where(a => a.CreatureId == query.CreatureId)
            .Select(a => a.AbilityName)
            .ToArrayAsync(cancellationToken);
    }
}
