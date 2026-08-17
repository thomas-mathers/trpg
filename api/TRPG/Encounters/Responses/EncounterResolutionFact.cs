namespace TRPG.Encounters.Responses;

[Tapper.TranspilationSource]
public enum EncounterResolutionOutcome
{
    Evaded,
    EvadeFailed,
    Retreated,
    RetreatFailed,
    Attacked,
}

[Tapper.TranspilationSource]
public record EncounterResolutionFact(
    Guid EncounterId,
    EncounterResolutionOutcome Outcome,
    string FactionName,
    string LocationName,
    IReadOnlyCollection<string> MemberNames
);
