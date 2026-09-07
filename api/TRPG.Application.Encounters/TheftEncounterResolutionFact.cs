namespace TRPG.Application.Encounters;

public enum TheftEncounterResolutionOutcome
{
    Apologized,
    Fled,
}

public record TheftEncounterResolutionFact(
    Guid EncounterId,
    TheftEncounterResolutionOutcome Outcome,
    string ConfrontingName,
    // "you" when the confronter owns the goods, so the narrator never invents an owner for them.
    string StolenFrom,
    IReadOnlyCollection<string> ItemNames,
    bool ItemsReturned,
    bool ItemsHeldByPlayer,
    bool LeftTheScene
);
