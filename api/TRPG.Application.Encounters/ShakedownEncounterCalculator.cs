using TRPG.Application.Configuration;

namespace TRPG.Application.Encounters;

internal static class ShakedownEncounterCalculator
{
    public static int ComputeTollGold(
        IReadOnlyCollection<int> memberLevels,
        ShakedownOptions options
    ) =>
        (int)
            Math.Clamp(
                Math.Ceiling(memberLevels.Sum() * options.TollGoldPerLevel),
                options.MinimumTollGold,
                options.MaximumTollGold
            );
}
