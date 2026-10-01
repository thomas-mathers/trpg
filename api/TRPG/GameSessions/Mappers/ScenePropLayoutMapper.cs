using TRPG.Application.Scenes.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class ScenePropLayoutMapper
{
    public static PropLayoutWire ToWire(this ScenePropLayout prop) =>
        new(prop.Id, prop.Model.ToResponse(), prop.Placement.ToWire(), prop.Footprint.ToWire());
}
