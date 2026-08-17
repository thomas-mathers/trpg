using Tapper;
using TRPG.GameSessions.Responses;
using TypedSignalR.Client;

namespace TRPG.Encounters.Responses;

[TranspilationSource]
public record HostileEncounterMemberState(string Name, CreatureType CreatureType, int Level);

[TranspilationSource]
public record HostileEncounterState(
    Guid EncounterId,
    string FactionName,
    string LocationName,
    IReadOnlyCollection<HostileEncounterMemberState> Members,
    IReadOnlyCollection<string> AllowedActions
);
