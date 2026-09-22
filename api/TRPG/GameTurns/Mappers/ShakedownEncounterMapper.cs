using TRPG.Domain.Models;
using TRPG.GameTurns.Tools;

namespace TRPG.GameTurns.Mappers;

internal static class ShakedownEncounterMapper
{
    public static MoveToolShakedownEncounter ToMoveToolSummary(this ShakedownEncounter encounter) =>
        new(
            encounter.FactionName,
            encounter.LocationName!,
            encounter.TollAmount,
            encounter
                .Members.Select(member => new MoveToolEncounterMember(
                    member.Name,
                    member.CreatureType,
                    member.Level
                ))
                .ToArray()
        );
}
