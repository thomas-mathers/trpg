namespace TRPG.Application.Abilities;

public static class CombatTiming
{
    public static readonly TimeSpan Round = TimeSpan.FromSeconds(6);

    public static TimeSpan Rounds(int count) => Round * count;
}
