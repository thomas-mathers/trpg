using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.Scenes.Commands;

namespace TRPG.Application.Scenes.Queries;

internal class GetSceneWithCatchUpQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
}

internal class GetSceneWithCatchUpQueryHandler(
    GetCreatureByIdQueryHandler getCreatureById,
    EnsureLocationCatchUpCommandHandler ensureLocationCatchUp,
    GetSceneQueryHandler getScene,
    IGameClientEventSink gameEvents
)
{
    public async Task<SceneResult?> Handle(
        GetSceneWithCatchUpQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = query.PlayerId },
            cancellationToken
        );

        if (player == null)
        {
            return null;
        }

        var catchUp = await ensureLocationCatchUp.Handle(
            new EnsureLocationCatchUpCommand
            {
                WorldId = query.WorldId,
                LocationId = player.LocationId,
                SessionId = query.SessionId,
            },
            cancellationToken
        );

        var scene = await getScene.Handle(
            new GetSceneQuery
            {
                WorldId = query.WorldId,
                PlayerId = query.PlayerId,
                CurrentDate = catchUp.CurrentDate,
            },
            cancellationToken
        );

        if (catchUp.CatchUpRan)
        {
            gameEvents.Enqueue(
                new SceneUpdatedEvent(
                    SceneSnapshotMapper.ToSnapshot(scene),
                    SceneUpdateReason.CatchUp
                )
            );
        }

        return scene;
    }
}
