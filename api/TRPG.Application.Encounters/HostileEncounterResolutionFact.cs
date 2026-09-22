namespace TRPG.Application.Encounters;

public enum HostileEncounterResolutionOutcome
{
    Fled,
    FleeFailed,
    Attacked,
}

public record HostileEncounterResolutionFact(
    Guid EncounterId,
    HostileEncounterResolutionOutcome Outcome,
    string FactionName,
    string LocationName,
    IReadOnlyCollection<string> MemberNames
);
