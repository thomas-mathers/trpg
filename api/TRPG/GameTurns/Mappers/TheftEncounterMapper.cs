using TRPG.Domain.Models;
using TRPG.GameTurns.Tools;

namespace TRPG.GameTurns.Mappers;

internal static class TheftEncounterMapper
{
    public static MoveToolOverdueKeyEncounter ToMoveToolSummary(this TheftEncounter encounter) =>
        new(encounter.ConfrontingName, encounter.ItemNames);
}
