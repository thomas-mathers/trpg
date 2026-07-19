namespace TRPG.Contracts.Combat.Responses;

public record FightState(IReadOnlyCollection<CombatantState> Combatants);

public record CombatantState(
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
    IReadOnlyCollection<ActiveDot> ActiveDots,
    IReadOnlyCollection<ActiveHot> ActiveHots,
    IReadOnlyCollection<ActiveBuff> ActiveBuffs
);

public record ActiveDot(string AbilityName, int Amount, string DamageType, int RemainingTurns);

public record ActiveHot(string AbilityName, int Amount, int RemainingTurns);

public record ActiveBuff(string Attribute, float Amount, string AmountType, int RemainingTurns);
