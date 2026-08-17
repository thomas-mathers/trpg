using Tapper;

namespace TRPG.Combat.ClientModels;

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
