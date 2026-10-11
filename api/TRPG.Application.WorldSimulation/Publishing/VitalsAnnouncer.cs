using TRPG.Application.Common.Queries;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Queries;

namespace TRPG.Application.WorldSimulation.Publishing;

internal class VitalsAnnouncer(
    IQueryHandler<
        GetActiveLocationPlayersQuery,
        IReadOnlyCollection<ActiveLocationPlayer>
    > getActiveLocationPlayers,
    CreatureVitalsPublisher vitalsPublisher
)
{
    public async Task Announce(
        Guid worldId,
        IReadOnlyCollection<CreatureRegenerated> regenerated,
        CancellationToken cancellationToken
    )
    {
        var players = await getActiveLocationPlayers.Handle(
            new GetActiveLocationPlayersQuery { WorldId = worldId },
            cancellationToken
        );

        foreach (var location in regenerated.GroupBy(simEvent => simEvent.LocationId))
        {
            await vitalsPublisher.Publish(
                worldId,
                location.Key,
                [
                    .. players
                        .Where(player => player.LocationId == location.Key)
                        .Select(player => player.PlayerId),
                ],
                [
                    .. location
                        .GroupBy(simEvent => simEvent.CreatureId)
                        .Select(creature => creature.Last().Vitals),
                ],
                cancellationToken
            );
        }
    }
}
