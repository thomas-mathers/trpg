using Tapper;

namespace TRPG.Encounters.Responses;

[TranspilationSource]
public record GuardEncounterState(
    Guid EncounterId,
    string GuardName,
    string LocationName,
    int FineAmount,
    int JailHours,
    IReadOnlyCollection<string> RecentOffenses
);
