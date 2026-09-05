using System.Text.Json;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Encounters.Results;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamHostileEncounterActionTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveHostileEncounterActionCommand,
        HostileEncounterActionResult
    > resolveHostileEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IGameClientEventSink gameEvents
)
    : EncounterActionTurnHandlerBase<
        HostileEncounter,
        HostileEncounterAction,
        HostileEncounterActionResult
    >(streamer, getActiveEncounter, refreshScene, getPlaytime, gameEvents)
{
    protected override async Task<HostileEncounterActionResult> Resolve(
        GameTurnSession session,
        HostileEncounter encounter,
        HostileEncounterAction action,
        CancellationToken cancellationToken
    ) =>
        await resolveHostileEncounterAction.Handle(
            new ResolveHostileEncounterActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                EncounterId = encounter.Id,
            },
            cancellationToken
        );

    protected override GameClientEvent BuildResolvedEvent(
        HostileEncounterActionResult resolution
    ) => new HostileEncounterResolvedEvent(resolution.Fact);

    protected override string BuildNarrationPrompt(
        HostileEncounterAction action,
        HostileEncounterActionResult resolution
    ) =>
        resolution.Fact.Outcome switch
        {
            HostileEncounterResolutionOutcome.Attacked
            or HostileEncounterResolutionOutcome.EvadeFailed
            or HostileEncounterResolutionOutcome.RetreatFailed =>
                $"The player chose to {DescribeAction(resolution.ActionKind)} the {resolution.Fact.FactionName} encounter, and it has erupted into a fight. Narrate only the confrontation beginning. The fight has not been resolved yet: do not describe who wins, who is hurt, or how it ends. Do not call any tools.",
            _ =>
                $"The player chose to {DescribeAction(resolution.ActionKind)} the {resolution.Fact.FactionName} encounter. Result: {JsonSerializer.Serialize(resolution.Fact, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Narrate the outcome vividly based on this result. Do not call any tools.",
        };

    private static string DescribeAction(HostileEncounterActionKind actionKind) =>
        actionKind switch
        {
            HostileEncounterActionKind.Attack => "attack",
            HostileEncounterActionKind.Evade => "evade",
            HostileEncounterActionKind.Retreat => "retreat from",
            _ => throw new ArgumentOutOfRangeException(nameof(actionKind)),
        };
}
