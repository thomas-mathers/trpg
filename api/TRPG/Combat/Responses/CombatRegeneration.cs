using Tapper;

namespace TRPG.Combat.Responses;

[TranspilationSource]
public record CombatRegeneration(
    Guid CombatantId,
    int PreviousAp,
    int CurrentAp,
    int MaximumAp,
    int PreviousMp,
    int CurrentMp,
    int MaximumMp
);
