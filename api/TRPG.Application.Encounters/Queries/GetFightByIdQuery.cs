using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetFightByIdQuery
{
    public required Guid FightId { get; init; }
}

internal class GetFightByIdQueryHandler(IEncountersDbContext context)
    : IQueryHandler<GetFightByIdQuery, FightEncounter?>
{
    public async Task<FightEncounter?> Handle(
        GetFightByIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Encounters.AsNoTracking()
            .OfType<FightEncounter>()
            .FirstOrDefaultAsync(f => f.Id == query.FightId, cancellationToken);
}
