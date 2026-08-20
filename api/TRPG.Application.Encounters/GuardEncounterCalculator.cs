using TRPG.Application.Configuration;

namespace TRPG.Application.Encounters;

internal static class GuardEncounterCalculator
{
    public static int ComputeFineGold(int reputationScore, GuardEncounterOptions options) =>
        (int)
            Math.Min(
                options.MaxFineGold,
                Math.Ceiling(Math.Abs(reputationScore) * options.FineGoldPerReputationPoint)
            );

    public static int ComputeJailHours(int reputationScore, GuardEncounterOptions options) =>
        (int)
            Math.Min(
                options.MaxJailHours,
                Math.Ceiling(Math.Abs(reputationScore) * options.JailHoursPerReputationPoint)
            );
}
