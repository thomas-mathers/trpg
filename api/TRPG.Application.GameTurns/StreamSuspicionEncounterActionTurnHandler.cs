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

internal class StreamSuspicionEncounterActionTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveSuspicionEncounterActionCommand,
        SuspicionEncounterResolutionFact
    > resolveSuspicionEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IGameClientEventSink gameEvents
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        SuspicionEncounterAction action,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, action, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        SuspicionEncounterAction action,
        CancellationToken cancellationToken
    )
    {
        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (encounter is not SuspicionEncounter suspicionEncounter)
        {
            return new GameTurnPrompt.Reply("There's no encounter to resolve right now.");
        }

        var resolution = await resolveSuspicionEncounterAction.Handle(
            new ResolveSuspicionEncounterActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                EncounterId = suspicionEncounter.Id,
                GuardCreatureId = suspicionEncounter.GuardCreatureId,
                GuardName = suspicionEncounter.GuardName,
                CityFactionId = suspicionEncounter.CityFactionId,
                EncounterLocationId = suspicionEncounter.LocationId,
                LocationName = suspicionEncounter.LocationName!,
            },
            cancellationToken
        );

        gameEvents.Enqueue(new SuspicionEncounterResolvedEvent(resolution));

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
        SuspicionEncounterAction action,
        SuspicionEncounterResolutionFact resolution
    )
    {
        var json = JsonSerializer.Serialize(
            resolution,
            TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
        );

        if (resolution.Outcome == SuspicionEncounterResolutionOutcome.FleeFailed)
        {
            return $"""
                The player tried to flee from {resolution.GuardName}'s questioning but was caught. Result: {json}.
                Narrate {resolution.GuardName} catching the player and turning noticeably harsher — this has
                escalated into a real confrontation over the fine or jail time. Do not narrate the confrontation's
                outcome (paying, jail, or resisting) or the player leaving; the encounter dialog is authoritative
                for what happens next and the client will show it after this response. Do not call any tools.
                """;
        }

        var actionDescription = DescribeAction(action);

        return $"""
            The player chose to {actionDescription} when questioned by {resolution.GuardName}. Result: {json}.
            Narrate the outcome vividly based on this result. Do not call any tools.
            """;
    }

    private static string DescribeAction(SuspicionEncounterAction action) =>
        action switch
        {
            ComplySuspicionAction => "comply",
            FleeSuspicionAction => "flee",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
