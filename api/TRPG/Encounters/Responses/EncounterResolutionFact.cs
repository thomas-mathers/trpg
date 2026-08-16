namespace TRPG.Encounters.Responses;

public enum EncounterResolutionOutcome
{
    Evaded,
    EvadeFailed,
    Retreated,
    RetreatFailed,
    Attacked,
}

public record EncounterResolutionFact(
    Guid EncounterId,
    EncounterResolutionOutcome Outcome,
    string FactionName,
    string LocationName,
    IReadOnlyCollection<string> MemberNames
);
