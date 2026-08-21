namespace TRPG.Encounters.Responses;

[Tapper.TranspilationSource]
public enum TheftEncounterResolutionOutcome
{
    Apologized,
    Fought,
}

[Tapper.TranspilationSource]
public record TheftEncounterResolutionFact(
    Guid EncounterId,
    TheftEncounterResolutionOutcome Outcome,
    string ConfrontingName,
    IReadOnlyCollection<string> ItemNames,
    bool ItemsReturned
);
