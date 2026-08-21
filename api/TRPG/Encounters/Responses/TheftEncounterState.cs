using Tapper;

namespace TRPG.Encounters.Responses;

[TranspilationSource]
public record TheftEncounterState(
    Guid EncounterId,
    string ConfrontingName,
    IReadOnlyCollection<string> ItemNames,
    IReadOnlyCollection<string> AllowedActions
);
