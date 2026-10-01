using TRPG.Application.Scenes.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneCreatureLayoutMapper
{
    public static CreatureLayoutWire ToWire(this SceneCreatureLayout creature) =>
        new(creature.Id, creature.Placement.ToWire());
}
