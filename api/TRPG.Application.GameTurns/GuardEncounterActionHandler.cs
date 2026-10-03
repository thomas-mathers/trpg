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

internal class GuardEncounterActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveGuardEncounterActionCommand,
        GuardEncounterResolutionFact
    > resolveGuardEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    IGameClientEventSink gameEvents
)
    : EncounterActionHandlerBase<
        GuardEncounter,
        GuardEncounterAction,
        GuardEncounterResolutionFact
    >(
        actionRunner,
        getActiveEncounter,
        refreshScene,
        publishEncounterStarted,
        getGameTime,
        gameEvents
    )
{
    protected override async Task<GuardEncounterResolutionFact> Resolve(
        GameTurnSession session,
        GuardEncounter encounter,
        GuardEncounterAction action,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        await resolveGuardEncounterAction.Handle(
            new ResolveGuardEncounterActionCommand
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
        GuardEncounterResolutionFact resolution
    ) => new GuardEncounterResolvedEvent(WorldId, resolution);
}
