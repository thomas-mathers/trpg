using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetCreatureByIdQueryHandler(TrpgDbContext context)
{
    public async Task<Creature?> Handle(
        GetCreatureByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context.Creatures.FindAsync([query.Id], cancellationToken);
    }
}
