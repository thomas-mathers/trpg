using TRPG.Domain.Models;
using TRPG.GameTurns.Tools;

namespace TRPG.GameTurns.Mappers;

internal static class HostileEncounterMapper
{
    public static MoveToolHostileEncounter ToMoveToolSummary(this HostileEncounter encounter) =>
        new(
            encounter.FactionName,
            encounter.LocationName!,
            encounter
                .Members.Select(member => new MoveToolEncounterMember(
                    member.Name,
                    member.CreatureType,
                    member.Level
                ))
                .ToArray()
        );
}
