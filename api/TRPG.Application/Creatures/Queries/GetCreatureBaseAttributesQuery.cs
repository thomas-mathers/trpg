using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureBaseAttributesQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureBaseAttributesQueryHandler(TrpgDbContext context)
{
    public async Task<Attributes> Handle(
        GetCreatureBaseAttributesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await context
            .Creatures.AsNoTracking()
            .FirstAsync(c => c.Id == query.CreatureId, cancellationToken);

        return creature.BaseAttributes;
    }
}
