using System.Text.Json;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamTheftEncounterNarrationTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    IGameClientEventSink gameEvents
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid encounterId,
        CancellationToken cancellationToken = default
    ) =>
        streamer.StreamTurn(
            session,
            ct => ResolveTurn(session, encounterId, ct),
            cancellationToken
        );

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        Guid encounterId,
        CancellationToken cancellationToken
    )
    {
        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (
            encounter is not TheftEncounter theftEncounter
            || theftEncounter.Id != encounterId
            || theftEncounter.WorldId != session.WorldId
        )
        {
            return new GameTurnPrompt.Reply("There's no theft encounter to resolve right now.");
        }

        gameEvents.Enqueue(new TheftEncounterStartedEvent(theftEncounter));

        var details = JsonSerializer.Serialize(
            new { theftEncounter.ConfrontingName, theftEncounter.ItemNames },
            Common.Serialization.TrpgJsonOptions.Default
        );
        return new GameTurnPrompt.Narrate(
            $"""
            The player was caught stealing. Details: {details}.
            Narrate the confrontation vividly from the confronting creature's perspective. The theft encounter
            has not been resolved: do not narrate an apology, a fight, the item's return, or the
            player leaving. Do not call any tools.
            """,
            IncludeTools: false
        );
    }
}
