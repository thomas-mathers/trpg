using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureWorldIdQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureWorldIdQueryHandler(TrpgDbContext context)
{
    public async Task<Guid> Handle(
        GetCreatureWorldIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature => creature.Id == query.CreatureId)
            .Select(creature => (Guid?)creature.WorldId)
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new EntityNotFoundException("Creature", query.CreatureId);
}
