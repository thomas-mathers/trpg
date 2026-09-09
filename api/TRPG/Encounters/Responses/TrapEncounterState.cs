using Tapper;

namespace TRPG.Encounters.Responses;

[TranspilationSource]
public enum TrapKind
{
    Mechanical,
    Collapse,
    Slope,
    Water,
}

[TranspilationSource]
public record TrapEncounterState(
    Guid EncounterId,
    TrapKind TrapKind,
    string? LocationName,
    IReadOnlyCollection<string> AllowedActions
);
