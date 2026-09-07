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
    IReadOnlyCollection<string> ItemNames,
    bool ItemsReturned,
    bool ItemsHeldByPlayer,
    bool LeftTheScene
);
