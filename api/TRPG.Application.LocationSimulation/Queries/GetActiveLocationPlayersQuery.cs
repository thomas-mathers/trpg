using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Worlds.Queries;

namespace TRPG.Application.LocationSimulation.Queries;

public class GetActiveLocationPlayersQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetActiveLocationPlayersQueryHandler(
    IQueryHandler<GetWorldPlayerIdQuery, Guid?> getWorldPlayerId,
    IQueryHandler<GetCreaturePresenceQuery, CreaturePresence?> getCreaturePresence
) : IQueryHandler<GetActiveLocationPlayersQuery, IReadOnlyCollection<ActiveLocationPlayer>>
{
    public async Task<IReadOnlyCollection<ActiveLocationPlayer>> Handle(
        GetActiveLocationPlayersQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var playerId = await getWorldPlayerId.Handle(
            new GetWorldPlayerIdQuery { WorldId = query.WorldId },
            cancellationToken
        );
        if (playerId == null)
        {
            return [];
        }

        var presence = await getCreaturePresence.Handle(
            new GetCreaturePresenceQuery { CreatureId = playerId.Value },
            cancellationToken
        );

        return presence == null
            ? []
            : [new ActiveLocationPlayer(presence.LocationId, playerId.Value, presence.Level)];
    }
}
