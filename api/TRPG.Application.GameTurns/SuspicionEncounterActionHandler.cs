using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class SuspicionEncounterActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveSuspicionEncounterActionCommand,
        SuspicionEncounterResolutionFact
    > resolveSuspicionEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    IGameClientEventSink gameEvents
)
    : EncounterActionHandlerBase<
        SuspicionEncounter,
        SuspicionEncounterAction,
        SuspicionEncounterResolutionFact
    >(
        actionRunner,
        getActiveEncounter,
        refreshScene,
        publishEncounterStarted,
        getGameTime,
        gameEvents
    )
{
    protected override async Task<SuspicionEncounterResolutionFact> Resolve(
        GameTurnSession session,
        SuspicionEncounter encounter,
        SuspicionEncounterAction action,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        await resolveSuspicionEncounterAction.Handle(
            new ResolveSuspicionEncounterActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                EncounterId = encounter.Id,
                GameTime = gameTime,
            },
            cancellationToken
        );

    protected override GameClientEvent BuildResolvedEvent(
        Guid WorldId,
        SuspicionEncounterResolutionFact resolution
    ) => new SuspicionEncounterResolvedEvent(WorldId, resolution);
}
