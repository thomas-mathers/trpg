using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

internal class GetUnallocatedAttributePointsQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetUnallocatedAttributePointsQueryHandler(
    TrpgDbContext context,
    StatFormulas statFormulas
)
{
    public async Task<int> Handle(
        GetUnallocatedAttributePointsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await context
            .Creatures.AsNoTracking()
            .FirstAsync(c => c.Id == query.CreatureId, cancellationToken);

        return statFormulas.CalculateUnallocatedAttributePoints(
            creature.BaseAttributes,
            creature.Level
        );
    }
}
