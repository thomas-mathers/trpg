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

internal class TheftEncounterActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveTheftEncounterActionCommand,
        TheftEncounterResolutionFact
    > resolveTheftEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    IGameClientEventSink gameEvents
)
    : EncounterActionHandlerBase<
        TheftEncounter,
        TheftEncounterAction,
        TheftEncounterResolutionFact
    >(
        actionRunner,
        getActiveEncounter,
        refreshScene,
        publishEncounterStarted,
        getGameTime,
        gameEvents
    )
{
    protected override async Task<TheftEncounterResolutionFact> Resolve(
        GameTurnSession session,
        TheftEncounter encounter,
        TheftEncounterAction action,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        await resolveTheftEncounterAction.Handle(
            new ResolveTheftEncounterActionCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                SessionId = session.SessionId,
                Action = action,
                EncounterId = encounter.Id,
                GameTime = gameTime,
            },
            cancellationToken
        );

    protected override GameClientEvent BuildResolvedEvent(
        TheftEncounterResolutionFact resolution
    ) => new TheftEncounterResolvedEvent(resolution);
}
