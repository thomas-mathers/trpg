namespace TRPG.Application.Worlds.Encounters;

internal enum HostileEncounterActionKind
{
    Attack,
    Evade,
    Retreat,
}

internal enum HostileEncounterActionOutcome
{
    Attacked,
    Evaded,
    EvadeFailed,
    Retreated,
    RetreatFailed,
}

internal static class HostileEncounterActionResolver
{
    private const int EvadeBaseChance = 50;
    private const int EvadeMaximumChance = 90;
    private const int RetreatBaseChance = 40;
    private const int RetreatMaximumChance = 85;
    private const int MinimumChance = 10;
    private const int LevelDifferenceMultiplier = 5;

    public static int EvadeSuccessChance(int playerLevel, int groupPower) =>
        Math.Clamp(
            EvadeBaseChance + (playerLevel - groupPower) * LevelDifferenceMultiplier,
            MinimumChance,
            EvadeMaximumChance
        );

    public static int RetreatSuccessChance(int playerLevel, int groupPower) =>
        Math.Clamp(
            RetreatBaseChance + (playerLevel - groupPower) * LevelDifferenceMultiplier,
            MinimumChance,
            RetreatMaximumChance
        );

    // roll (0-99) is caller-supplied rather than rolled internally, so this stays pure and testable.
    public static HostileEncounterActionOutcome Resolve(
        HostileEncounterActionKind action,
        int playerLevel,
        int groupPower,
        int roll
    ) =>
        action switch
        {
            HostileEncounterActionKind.Attack => HostileEncounterActionOutcome.Attacked,
            HostileEncounterActionKind.Evade => roll < EvadeSuccessChance(playerLevel, groupPower)
                ? HostileEncounterActionOutcome.Evaded
                : HostileEncounterActionOutcome.EvadeFailed,
            HostileEncounterActionKind.Retreat => roll
            < RetreatSuccessChance(playerLevel, groupPower)
                ? HostileEncounterActionOutcome.Retreated
                : HostileEncounterActionOutcome.RetreatFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
