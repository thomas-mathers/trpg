using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Encounters.Responses;

public record HostileEncounterMemberState(string Name, CreatureType CreatureType, int Level);

public record HostileEncounterState(
    Guid EncounterId,
    string FactionName,
    string LocationName,
    IReadOnlyCollection<HostileEncounterMemberState> Members,
    IReadOnlyCollection<string> AllowedActions
);
