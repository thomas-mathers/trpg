using TRPG.Application.Scenes.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneBoxLayoutMapper
{
    public static BoxLayoutWire ToWire(this SceneBoxLayout box) =>
        new(box.Id, box.Placement.ToWire(), box.Footprint.ToWire());
}
