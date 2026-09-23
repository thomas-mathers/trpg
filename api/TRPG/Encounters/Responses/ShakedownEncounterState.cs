using Tapper;

namespace TRPG.Encounters.Responses;

[TranspilationSource]
public record ShakedownEncounterState(
    Guid EncounterId,
    string FactionName,
    string LocationName,
    int TollAmount,
    IReadOnlyCollection<HostileEncounterMemberState> Members,
    IReadOnlyCollection<string> AllowedActions,
    bool CanAffordToll
);
