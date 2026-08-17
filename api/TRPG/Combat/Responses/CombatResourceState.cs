using Tapper;

namespace TRPG.Combat.Responses;

[TranspilationSource]
public record CombatResourceState(
    Guid CombatantId,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp
);
