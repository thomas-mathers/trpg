namespace TRPG.Contracts.Combat.Responses;

public record CombatSnapshot(IReadOnlyCollection<CombatantStatusSnapshot> Combatants);

public record CombatantStatusSnapshot(
    string Name,
    bool IsPlayer,
    bool IsAlive,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp,
    IReadOnlyDictionary<string, int> ActiveConditions,
    IReadOnlyCollection<ActiveDotStatus> ActiveDots,
    IReadOnlyCollection<ActiveHotStatus> ActiveHots,
    IReadOnlyCollection<ActiveBuffStatus> ActiveBuffs
);

public record ActiveDotStatus(string AbilityName, int Amount, string DamageType, int RemainingTurns);

public record ActiveHotStatus(string AbilityName, int Amount, int RemainingTurns);

public record ActiveBuffStatus(string Attribute, float Amount, string AmountType, int RemainingTurns);
