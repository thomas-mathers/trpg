using TRPG.Data.Models;

namespace TRPG.Application.Encounters;

public record HostileEncounterMember(string Name, CreatureType CreatureType, int Level);

public record HostileEncounterState(
    Guid EncounterId,
    string FactionName,
    string LocationName,
    IReadOnlyCollection<HostileEncounterMember> Members,
    IReadOnlyCollection<string> AllowedActions
);
