namespace TRPG.Application.Encounters;

public record GuardEncounterState(
    Guid EncounterId,
    string GuardName,
    string LocationName,
    int FineAmount,
    int JailHours,
    IReadOnlyCollection<string> RecentOffenses
);
