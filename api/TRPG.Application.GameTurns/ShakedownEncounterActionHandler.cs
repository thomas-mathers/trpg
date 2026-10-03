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

internal class ShakedownEncounterActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveShakedownEncounterActionCommand,
        ShakedownEncounterResolutionFact
    > resolveShakedownEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    IGameClientEventSink gameEvents
)
    : EncounterActionHandlerBase<
        ShakedownEncounter,
        ShakedownEncounterAction,
        ShakedownEncounterResolutionFact
    >(
        actionRunner,
        getActiveEncounter,
        refreshScene,
        publishEncounterStarted,
        getGameTime,
        gameEvents
    )
{
    protected override async Task<ShakedownEncounterResolutionFact> Resolve(
        GameTurnSession session,
        ShakedownEncounter encounter,
        ShakedownEncounterAction action,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        await resolveShakedownEncounterAction.Handle(
            new ResolveShakedownEncounterActionCommand
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
        ShakedownEncounterResolutionFact resolution
    ) => new ShakedownEncounterResolvedEvent(WorldId, resolution);
}
