using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Queries;
using TRPG.Domain;

namespace TRPG.Application.GameSessions.Queries;

public class GetGameTimeByWorldIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetGameTimeByWorldIdQueryHandler(IWorldClock worldClock)
    : IQueryHandler<GetGameTimeByWorldIdQuery, GameInstant>
{
    public async Task<GameInstant> Handle(
        GetGameTimeByWorldIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await worldClock.GetCurrent(query.WorldId, cancellationToken);
    }
}
