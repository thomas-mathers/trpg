using Tapper;

namespace TRPG.Combat.ClientModels;

[TranspilationSource]
public record CombatResourceState(
    Guid CombatantId,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp
);
