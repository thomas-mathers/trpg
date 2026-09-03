using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.GameSessions.Queries;

public class GetTrespassingBuildingIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetTrespassingBuildingIdQueryHandler(IGameSessionsDbContext context)
    : IQueryHandler<GetTrespassingBuildingIdQuery, Guid?>
{
    public async Task<Guid?> Handle(
        GetTrespassingBuildingIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .GameSessions.AsNoTracking()
            .Where(s => s.WorldId == query.WorldId)
            .Select(s => s.TrespassingBuildingId)
            .FirstOrDefaultAsync(cancellationToken);
}
