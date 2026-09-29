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

public static class CreatureVitalsExtensions
{
    public static bool HasDied(this IEnumerable<CreatureVitals> vitals, Guid creatureId) =>
        vitals.Any(v => v.CreatureId == creatureId && v.CurrentHp <= 0);
}
