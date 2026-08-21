namespace TRPG.Application.Encounters;

public enum TheftEncounterResolutionOutcome
{
    Apologized,
    Fought,
}

public record TheftEncounterResolutionFact(
    Guid EncounterId,
    TheftEncounterResolutionOutcome Outcome,
    string OwnerName,
    IReadOnlyCollection<string> ItemNames,
    bool ItemsReturned
);
