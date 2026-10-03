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

internal class TrapEncounterActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveTrapEncounterActionCommand,
        TrapEncounterResolutionFact
    > resolveTrapEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    IGameClientEventSink gameEvents
)
    : EncounterActionHandlerBase<TrapEncounter, TrapEncounterAction, TrapEncounterResolutionFact>(
        actionRunner,
        getActiveEncounter,
        refreshScene,
        publishEncounterStarted,
        getGameTime,
        gameEvents
    )
{
    protected override async Task<TrapEncounterResolutionFact> Resolve(
        GameTurnSession session,
        TrapEncounter encounter,
        TrapEncounterAction action,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        await resolveTrapEncounterAction.Handle(
            new ResolveTrapEncounterActionCommand
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
        Guid WorldId,
        TrapEncounterResolutionFact resolution
    ) => new TrapEncounterResolvedEvent(WorldId, resolution);
}
