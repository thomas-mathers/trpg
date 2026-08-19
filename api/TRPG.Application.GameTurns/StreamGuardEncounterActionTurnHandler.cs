using System.Text.Json;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamGuardEncounterActionTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveGuardEncounterQuery, GuardEncounter?> getActiveGuardEncounter,
    ICommandHandler<
        ResolveGuardEncounterActionCommand,
        GuardEncounterResolutionFact
    > resolveGuardEncounterAction,
    IGameClientEventSink gameEvents
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        GuardEncounterAction action,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, action, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        GuardEncounterAction action,
        CancellationToken cancellationToken
    )
    {
        var encounter = await getActiveGuardEncounter.Handle(
            new GetActiveGuardEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (encounter == null)
        {
            return new GameTurnPrompt.Reply("There's no guard encounter to resolve right now.");
        }

        var resolution = await resolveGuardEncounterAction.Handle(
            new ResolveGuardEncounterActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                EncounterId = encounter.Id,
                GuardCreatureId = encounter.GuardCreatureId,
                EncounterLocationId = encounter.LocationId,
            },
            cancellationToken
        );

        gameEvents.Enqueue(new GuardEncounterResolvedEvent(resolution));

        return new GameTurnPrompt.Narrate(
            $"The player chose to {DescribeAction(resolution.Outcome)} with guard {resolution.GuardName}. Result: {JsonSerializer.Serialize(resolution, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Narrate the outcome vividly based on this result. Do not call any tools.",
            IncludeTools: false
        );
    }

    private static string DescribeAction(GuardEncounterResolutionOutcome outcome) =>
        outcome switch
        {
            GuardEncounterResolutionOutcome.PaidFine => "pay the fine",
            GuardEncounterResolutionOutcome.WentToJail => "go peacefully to jail",
            GuardEncounterResolutionOutcome.ResistedArrest => "resist arrest",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
}
