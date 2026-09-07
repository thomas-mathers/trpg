using TRPG.Domain.Models;
using TRPG.Encounters.Mappers;
using TRPG.GameTurns.Tools;

namespace TRPG.GameTurns.Mappers;

internal static class GuardEncounterMapper
{
    public static MoveToolGuardEncounter ToMoveToolSummary(this GuardEncounter encounter) =>
        new(
            encounter.GuardName,
            encounter.LocationName!,
            encounter.FineAmount,
            encounter.JailHours,
            encounter
                .RecentOffenses.Select(offense => new MoveToolGuardOffense(
                    offense.ToText(),
                    offense.SubjectIsTheGuard
                ))
                .ToArray()
        );
}
