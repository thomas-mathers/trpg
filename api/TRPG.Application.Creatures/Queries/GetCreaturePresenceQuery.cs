using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Creatures.Queries;

public class GetCreaturePresenceQuery
{
    public required Guid CreatureId { get; init; }
}

public record CreaturePresence(Guid LocationId, int Level);

internal class GetCreaturePresenceQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetCreaturePresenceQuery, CreaturePresence?>
{
    public async Task<CreaturePresence?> Handle(
        GetCreaturePresenceQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature => creature.Id == query.CreatureId)
            .Select(creature => new CreaturePresence(creature.LocationId, creature.Level))
            .FirstOrDefaultAsync(cancellationToken);
}
