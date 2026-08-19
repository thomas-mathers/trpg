namespace TRPG.Application.Encounters;

public enum HostileEncounterResolutionOutcome
{
    Evaded,
    EvadeFailed,
    Retreated,
    RetreatFailed,
    Attacked,
}

public record HostileEncounterResolutionFact(
    Guid EncounterId,
    HostileEncounterResolutionOutcome Outcome,
    string FactionName,
    string LocationName,
    IReadOnlyCollection<string> MemberNames
);
