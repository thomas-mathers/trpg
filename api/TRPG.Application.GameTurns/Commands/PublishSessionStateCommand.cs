using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Publishing;
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
    IGameClientEventDispatcher eventDispatcher,
    ICommandHandler<PublishPlayerSceneCommand> publishPlayerScene,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    ICommandHandler<PublishCombatStateCommand> publishCombatState,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    WorldSimulationCoordinator simulation
) : ICommandHandler<PublishSessionStateCommand>
{
    public async Task Handle(
        PublishSessionStateCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await simulation.Request(
            command.WorldId,
            (_, token) => EnqueueSessionState(command, token),
            cancellationToken
        );

        await eventDispatcher.FlushAsync(command.WorldId, cancellationToken);
    }

    private async Task EnqueueSessionState(
        PublishSessionStateCommand command,
        CancellationToken cancellationToken
    )
    {
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        await publishPlayerScene.Handle(
            new PublishPlayerSceneCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );

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
    }
}
