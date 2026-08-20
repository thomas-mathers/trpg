using System.Text.Json;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Encounters.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamHostileEncounterActionTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveHostileEncounterActionCommand,
        HostileEncounterActionResult
    > resolveHostileEncounterAction,
    IGameClientEventSink gameEvents
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        HostileEncounterAction action,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, action, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        HostileEncounterAction action,
        CancellationToken cancellationToken
    )
    {
        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (encounter is not HostileEncounter hostileEncounter)
        {
            return new GameTurnPrompt.Reply("There's no encounter to resolve right now.");
        }

        var resolution = await resolveHostileEncounterAction.Handle(
            new ResolveHostileEncounterActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                EncounterId = hostileEncounter.Id,
                FactionName = hostileEncounter.FactionName,
                LocationName = hostileEncounter.LocationName!,
                Members = hostileEncounter.Members,
                ArrivalOriginLocationId = hostileEncounter.ArrivalOriginLocationId,
            },
            cancellationToken
        );

        EnqueueEncounterResolutionEvents(resolution);

        return new GameTurnPrompt.Narrate(
            $"The player chose to {DescribeAction(resolution.ActionKind)} the {resolution.Fact.FactionName} encounter. Result: {JsonSerializer.Serialize(resolution.Fact, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Narrate the outcome vividly based on this result. Do not call any tools.",
            IncludeTools: false
        );
    }

    private void EnqueueEncounterResolutionEvents(HostileEncounterActionResult resolution)
    {
        gameEvents.Enqueue(new HostileEncounterResolvedEvent(resolution.Fact));
    }

    private static string DescribeAction(HostileEncounterActionKind actionKind) =>
        actionKind switch
        {
            HostileEncounterActionKind.Attack => "attack",
            HostileEncounterActionKind.Evade => "evade",
            HostileEncounterActionKind.Retreat => "retreat from",
            _ => throw new ArgumentOutOfRangeException(nameof(actionKind)),
        };
}
