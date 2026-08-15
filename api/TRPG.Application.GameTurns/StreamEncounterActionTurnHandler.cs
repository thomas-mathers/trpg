using System.Text.Json;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Handling;
using TRPG.Application.Common.Tools;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions;
using TRPG.Contracts.Encounters.Requests;
using TRPG.Data.Models;

namespace TRPG.Application.GameTurns;

internal class StreamEncounterActionTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveEncounterQuery, HostileEncounter?> getActiveEncounter,
    ICommandHandler<
        ResolveEncounterActionCommand,
        EncounterActionResolution
    > resolveEncounterAction,
    IGameClientEventSink gameEvents
)
{
    public IAsyncEnumerable<string> Handle(
        GameSessionIdentity session,
        PlayerEncounterAction action,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, action, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameSessionIdentity session,
        PlayerEncounterAction action,
        CancellationToken cancellationToken
    )
    {
        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (encounter == null)
        {
            return new GameTurnPrompt.Reply("There's no encounter to resolve right now.");
        }

        var resolution = await resolveEncounterAction.Handle(
            new ResolveEncounterActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                EncounterId = encounter.Id,
                EncounterGroupId = encounter.EncounterGroupId,
                EncounterLocationId = encounter.LocationId,
                ArrivalOriginLocationId = encounter.ArrivalOriginLocationId,
            },
            cancellationToken
        );

        EnqueueEncounterResolutionEvents(resolution);

        return new GameTurnPrompt.Narrate(
            $"The player chose to {DescribeAction(resolution.ActionKind)} the {resolution.Fact.FactionName} encounter. Result: {JsonSerializer.Serialize(resolution.Fact, TRPG.Contracts.TrpgJsonOptions.Default)}. Narrate the outcome vividly based on this result. Do not call any tools.",
            IncludeTools: false
        );
    }

    private void EnqueueEncounterResolutionEvents(EncounterActionResolution resolution)
    {
        gameEvents.Enqueue(new EncounterResolvedEvent(resolution.Fact));

        if (resolution.Combatants != null)
        {
            gameEvents.Enqueue(new CombatStartedEvent(resolution.Combatants));
        }
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
