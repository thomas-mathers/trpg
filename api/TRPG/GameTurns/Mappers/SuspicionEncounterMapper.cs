using TRPG.Domain.Models;
using TRPG.GameTurns.Tools;

namespace TRPG.GameTurns.Mappers;

internal static class SuspicionEncounterMapper
{
    public static MoveToolSuspicionEncounter ToMoveToolSummary(this SuspicionEncounter encounter) =>
        new(encounter.GuardName, encounter.LocationName!, DescribeCause(encounter.Cause));

    private static string DescribeCause(SuspicionCause cause) =>
        cause switch
        {
            SuspicionCause.Sneaking =>
                "the player was noticed moving furtively, as if trying not to be seen",
            SuspicionCause.CastingMagicInPublic =>
                "the player was noticed casting magic openly in public",
        };
}
