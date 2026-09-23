namespace TRPG.Encounters.Responses;

[Tapper.TranspilationSource]
public enum ShakedownEncounterResolutionOutcome
{
    Intimidated,
    IntimidateFailed,
    PaidToll,
    Fought,
    Fled,
    FleeFailed,
}

[Tapper.TranspilationSource]
public record ShakedownEncounterResolutionFact(
    Guid EncounterId,
    ShakedownEncounterResolutionOutcome Outcome,
    string FactionName,
    string LocationName,
    int TollAmount,
    IReadOnlyCollection<string> MemberNames
);
