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
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IGameClientEventSink gameEvents
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        TheftEncounterAction action,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, action, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        TheftEncounterAction action,
        CancellationToken cancellationToken
    )
    {
        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (
            encounter is not TheftEncounter theftEncounter
            || theftEncounter.WorldId != session.WorldId
        )
        {
            return new GameTurnPrompt.Reply("There's no theft encounter to resolve right now.");
        }

        var resolution = await resolveTheftEncounterAction.Handle(
            new ResolveTheftEncounterActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                EncounterId = theftEncounter.Id,
            },
            cancellationToken
        );

        gameEvents.Enqueue(new TheftEncounterResolvedEvent(resolution));

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Playtime = playtime,
            },
            cancellationToken
        );

        return new GameTurnPrompt.Narrate(
            BuildNarrationPrompt(action, resolution),
            IncludeTools: false
        );
    }

    private static string BuildNarrationPrompt(
        TheftEncounterAction action,
        TheftEncounterResolutionFact resolution
    ) =>
        action switch
        {
            FleeTheftEncounterAction =>
                $"The player chose to flee after {resolution.ConfrontingName} caught them stealing. Narrate them wrenching free and getting away with the item before {resolution.ConfrontingName} can stop them — no violence occurs. Do not call any tools.",
            ApologizeTheftEncounterAction =>
                $"The player chose to apologize after {resolution.ConfrontingName} caught them stealing. Result: {JsonSerializer.Serialize(resolution, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Narrate the outcome vividly based on this result. This resolves the confrontation only — the player has not gone anywhere; narrate them as still standing right where they were caught, not as having left or arrived somewhere new. Do not call any tools.",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
