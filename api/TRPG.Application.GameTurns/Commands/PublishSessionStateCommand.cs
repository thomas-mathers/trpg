using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.GameTurns.Queries;
using TRPG.Application.GameTurns.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Commands;

public class PublishSessionStateCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
}

internal class PublishSessionStateCommandHandler(
    ScenePublisher scenePublisher,
    IGameClientEventDispatcher eventDispatcher,
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    IQueryHandler<GetCurrentSceneQuery, SceneResult> getCurrentScene,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    ICommandHandler<PublishCombatStateCommand> publishCombatState,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted
) : ICommandHandler<PublishSessionStateCommand>
{
    public async Task Handle(
        PublishSessionStateCommand command,
        CancellationToken cancellationToken = default
    )
    {
        // Stamped before the scene is read so a later-numbered snapshot never describes older state.
        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = command.WorldId },
            cancellationToken
        );

        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        var scene = await getCurrentScene.Handle(
            new GetCurrentSceneQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                GameTime = gameTime,
            },
            cancellationToken
        );
        scenePublisher.Publish(command.PlayerId, scene, stamp);

        await publishCombatState.Handle(
            new PublishCombatStateCommand { PlayerId = command.PlayerId },
            cancellationToken
        );

        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = command.PlayerId,
                Encounter = encounter,
                GameTime = gameTime,
            },
            cancellationToken
        );

        await eventDispatcher.FlushAsync(command.WorldId, cancellationToken);
    }
}
