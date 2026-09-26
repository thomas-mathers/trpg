namespace TRPG.Creatures.Responses;

[Tapper.TranspilationSource]
public record PlayerVitalsUpdated(
    Guid PlayerId,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp,
    long Version
);
