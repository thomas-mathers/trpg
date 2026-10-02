using TRPG.Domain.Models;
using TRPG.Encounters.Mappers;
using TRPG.GameTurns.Tools;

namespace TRPG.GameTurns.Mappers;

internal static class GuardEncounterMapper
{
    public static GuardEncounterSummary ToToolSummary(this GuardEncounter encounter) =>
        new(
            encounter.GuardName,
            encounter.LocationName!,
            encounter.FineAmount,
            encounter.JailHours,
            encounter
                .RecentOffenses.Select(offense => new GuardOffenseSummary(
                    offense.ToText(),
                    offense.SubjectIsTheGuard
                ))
                .ToArray()
        );
}
