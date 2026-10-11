namespace TRPG.Creatures.Responses;

[Tapper.TranspilationSource]
public record CreatureVitalsUpdated(
    Guid WorldId,
    Guid LocationId,
    IReadOnlyCollection<CreatureVitalsEntry> Entries,
    long Version
);

[Tapper.TranspilationSource]
public record CreatureVitalsEntry(
    Guid CreatureId,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp
);
