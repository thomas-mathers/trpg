namespace TRPG.Encounters.Responses;

[Tapper.TranspilationSource]
public enum HostileEncounterResolutionOutcome
{
    Evaded,
    EvadeFailed,
    Retreated,
    RetreatFailed,
    Attacked,
}

[Tapper.TranspilationSource]
public record HostileEncounterResolutionFact(
    Guid EncounterId,
    HostileEncounterResolutionOutcome Outcome,
    string FactionName,
    string LocationName,
    IReadOnlyCollection<string> MemberNames
);
