using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Queries;

public class GetFightByIdQuery
{
    public required Guid FightId { get; init; }
}

internal class GetFightByIdQueryHandler(TrpgDbContext context)
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
