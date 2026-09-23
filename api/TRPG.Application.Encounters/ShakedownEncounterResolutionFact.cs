namespace TRPG.Application.Encounters;

public enum ShakedownEncounterResolutionOutcome
{
    Intimidated,
    IntimidateFailed,
    PaidToll,
    Fought,
    Fled,
    FleeFailed,
}

public record ShakedownEncounterResolutionFact(
    Guid EncounterId,
    ShakedownEncounterResolutionOutcome Outcome,
    string FactionName,
    string LocationName,
    int TollAmount,
    IReadOnlyCollection<string> MemberNames
);
