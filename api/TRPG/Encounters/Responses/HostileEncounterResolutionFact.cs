namespace TRPG.Encounters.Responses;

[Tapper.TranspilationSource]
public enum HostileEncounterResolutionOutcome
{
    Fled,
    FleeFailed,
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
