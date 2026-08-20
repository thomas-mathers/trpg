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
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
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
        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (encounter is not GuardEncounter guardEncounter)
        {
            return new GameTurnPrompt.Reply("There's no encounter to resolve right now.");
        }

        var resolution = await resolveGuardEncounterAction.Handle(
            new ResolveGuardEncounterActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                EncounterId = guardEncounter.Id,
                GuardCreatureId = guardEncounter.GuardCreatureId,
                CityFactionId = guardEncounter.CityFactionId,
                GuardName = guardEncounter.GuardName,
                EncounterLocationId = guardEncounter.LocationId,
                LocationName = guardEncounter.LocationName!,
                ReputationScore = guardEncounter.ReputationScore,
                FineAmount = guardEncounter.FineAmount,
                JailHours = guardEncounter.JailHours,
            },
            cancellationToken
        );

        gameEvents.Enqueue(new GuardEncounterResolvedEvent(resolution));

        return new GameTurnPrompt.Narrate(
            $"The player chose to {DescribeAction(action)} with {resolution.GuardName}. Result: {JsonSerializer.Serialize(resolution, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Narrate the outcome vividly based on this result. Do not call any tools.",
            IncludeTools: false
        );
    }

    private static string DescribeAction(GuardEncounterAction action) =>
        action switch
        {
            PayFineEncounterAction => "pay the fine",
            GoToJailEncounterAction => "go to jail",
            ResistArrestEncounterAction => "resist arrest",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
