using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureBaseAttributesQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureBaseAttributesQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCreatureBaseAttributesQuery, Attributes>
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
