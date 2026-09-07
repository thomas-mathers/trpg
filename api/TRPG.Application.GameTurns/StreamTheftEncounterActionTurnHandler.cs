using System.Text.Json;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamTheftEncounterActionTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveTheftEncounterActionCommand,
        TheftEncounterResolutionFact
    > resolveTheftEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IGameClientEventSink gameEvents
)
    : EncounterActionTurnHandlerBase<
        TheftEncounter,
        TheftEncounterAction,
        TheftEncounterResolutionFact
    >(streamer, getActiveEncounter, refreshScene, publishEncounterStarted, getPlaytime, gameEvents)
{
    protected override async Task<TheftEncounterResolutionFact> Resolve(
        GameTurnSession session,
        TheftEncounter encounter,
        TheftEncounterAction action,
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
            },
            cancellationToken
        );

    protected override GameClientEvent BuildResolvedEvent(
        TheftEncounterResolutionFact resolution
    ) => new TheftEncounterResolvedEvent(resolution);

    protected override string BuildNarrationPrompt(
        TheftEncounterAction action,
        TheftEncounterResolutionFact resolution
    ) =>
        action switch
        {
            FleeTheftEncounterAction =>
                $"The player chose to flee after {resolution.ConfrontingName} caught them stealing. Result: {JsonSerializer.Serialize(resolution, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Narrate them wrenching free and getting clear before {resolution.ConfrontingName} can stop them — no violence occurs. Narrate strictly from the result: when itemsHeldByPlayer is false they leave empty-handed and the items never left their owner, and when leftTheScene is false they are still standing where they were caught rather than having gone anywhere. Do not call any tools.",
            ApologizeTheftEncounterAction =>
                $"The player chose to apologize after {resolution.ConfrontingName} caught them stealing. Result: {JsonSerializer.Serialize(resolution, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Narrate the outcome vividly based on this result. This resolves the confrontation only — the player has not gone anywhere; narrate them as still standing right where they were caught, not as having left or arrived somewhere new. Do not call any tools.",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
