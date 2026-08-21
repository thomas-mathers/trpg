namespace TRPG.Application.Encounters;

public enum TheftEncounterResolutionOutcome
{
    Apologized,
    Fought,
}

public record TheftEncounterResolutionFact(
    Guid EncounterId,
    TheftEncounterResolutionOutcome Outcome,
    string ConfrontingName,
    IReadOnlyCollection<string> ItemNames,
    bool ItemsReturned
);
