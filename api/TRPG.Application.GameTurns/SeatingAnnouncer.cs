using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Events;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class SeatingAnnouncer(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    ScenePublisher scenePublisher
)
{
    public async Task Announce(
        GameTurnSession session,
        Guid? seatId,
        CancellationToken cancellationToken
    )
    {
        // Stamped before the creature is read so the event never describes older state than its version.
        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = session.WorldId },
            cancellationToken
        );
        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = session.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), session.PlayerId);

        scenePublisher.PublishSeating(
            session.PlayerId,
            new SeatingChangedEvent(
                session.WorldId,
                player.LocationId,
                stamp,
                player.Id,
                seatId,
                player.Posture,
                new Placement(player.X, player.Y, player.Angle)
            )
        );
    }
}
