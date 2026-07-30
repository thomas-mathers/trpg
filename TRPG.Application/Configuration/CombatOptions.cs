namespace TRPG.Application.Configuration;

public class CombatOptions
{
    public float ApRegenPercentPerRound { get; init; } = 0.25f;
    public float MpRegenPercentPerRound { get; init; } = 0.10f;
    public int BaseProficiency { get; init; } = 50;

    // Non-player combatants never accumulate proficiency through play the way the player does
    // (see AdjustWeaponProficienciesCommandHandler, which only ever updates the player's own
    // row) - a purely formulaic stand-in instead, fitted to track average player Defense+Evasion
    // by level (see TRPG.Balance's DefenseCurveExperiment) so non-player hit chance keeps pace
    // with real player defense instead of sitting frozen at BaseProficiency forever.
    public float NonPlayerProficiencyBase { get; init; } = 113f;
    public float NonPlayerProficiencyPerLevel { get; init; } = 4.7f;
    public float MinHitChance { get; init; } = 0.05f;
    public float MaxHitChance { get; init; } = 0.95f;
    public float StrengthDamageBonusPerPoint { get; init; } = 0.01f;

    // Physical damage is inherently capped by weapon roll, so its Strength bonus stays linear.
    // Magic damage has no such ceiling, so Intelligence's bonus grows logarithmically instead of
    // linearly - matches the linear per-point rate around Intelligence = IntelligenceDamageLogDivisor,
    // then flattens out for high-investment casters instead of scaling without bound.
    public float IntelligenceDamageLogDivisor { get; init; } = 50f;
    public float MaxResistancePercent { get; init; } = 0.75f;

    // A defender's own Dexterity makes them harder to hit, on top of Defense - lets
    // high-Dexterity, low-Defense builds (Rogue, Ranger) actually benefit defensively from the
    // stat they invest in most heavily, instead of Dexterity only ever affecting turn order.
    // Kept well below 1:1 with Defense (a modest evasion bonus, not a Defense substitute) so
    // heavy-armor classes' itemization still matters.
    public float EvasionPerDexterityPoint { get; init; } = 0.6f;

    // Snared previously had no gameplay effect at all - applied and displayed, never read
    // anywhere. Hobbling the snared creature's own effective Dexterity gives it a real cost
    // wherever Dexterity matters (evasion, crit chance) without needing a new incapacitation
    // check alongside Frozen/Stunned/Blinded/Silenced.
    public float SnareDexterityReductionPercent { get; init; } = 0.5f;
    public float LowResourceThresholdPercent { get; init; } = 0.3f;
    public float OpeningBuffChancePercent { get; init; } = 0.5f;

    // A second, universal payoff for Dexterity investment beyond evasion/turn order - lets
    // finesse-built attackers (Rogue, Ranger) convert their heaviest stat directly into damage
    // without changing the Strength-based physical damage formula itself. Applies to every damage
    // type rather than gating on Physical, so it stays a flat bonus rather than a special case.
    public float CritChancePerDexterityPoint { get; init; } = 0.002f;
    public float MaxCritChance { get; init; } = 0.5f;
    public float CritDamageMultiplier { get; init; } = 2.5f;
}
