using TRPG.Application.Configuration;

namespace TRPG.Application.Encounters;

internal static class GuardEncounterCalculator
{
    public static int ComputeFineGold(int reputationScore, GuardEncounterOptions options) =>
        (int)
            Math.Clamp(
                Math.Ceiling(Math.Abs(reputationScore) * options.FineGoldPerReputationPoint),
                options.MinimumFineGold,
                options.MaxFineGold
            );

    public static int ComputeJailHours(int reputationScore, GuardEncounterOptions options) =>
        (int)
            Math.Clamp(
                Math.Ceiling(Math.Abs(reputationScore) * options.JailHoursPerReputationPoint),
                options.MinimumJailHours,
                options.MaxJailHours
            );
}
