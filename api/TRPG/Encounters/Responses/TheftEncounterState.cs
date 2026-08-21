using Tapper;

namespace TRPG.Encounters.Responses;

[TranspilationSource]
public record TheftEncounterState(
    Guid EncounterId,
    string OwnerName,
    IReadOnlyCollection<string> ItemNames,
    IReadOnlyCollection<string> AllowedActions
);
