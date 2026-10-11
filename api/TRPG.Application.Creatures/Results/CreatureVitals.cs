namespace TRPG.Application.Creatures.Results;

public record CreatureVitals(
    Guid CreatureId,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp
);
