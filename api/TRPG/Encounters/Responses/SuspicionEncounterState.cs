using Tapper;

namespace TRPG.Encounters.Responses;

[TranspilationSource]
public enum SuspicionCause
{
    Sneaking,
    CastingMagicInPublic,
}

[TranspilationSource]
public record SuspicionEncounterState(
    Guid EncounterId,
    string GuardName,
    string LocationName,
    SuspicionCause Cause,
    IReadOnlyCollection<string> AllowedActions
);
