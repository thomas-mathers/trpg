using TRPG.Application.Encounters.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Results;

public record TheftEncounterPromptFacts(
    string ConfrontingName,
    string StolenFrom,
    IReadOnlyCollection<string> ItemNames,
    bool ItemsHeldByPlayer
);

public static class TheftEncounterPromptFactsMapper
{
    public static TheftEncounterPromptFacts ToPromptFacts(this TheftEncounter encounter) =>
        new(
            encounter.ConfrontingName,
            encounter.ToStolenFrom(),
            encounter.ItemNames,
            ItemsHeldByPlayer: encounter.ItemSelections.Count > 0
        );
}
