using TRPG.Application.Scenes.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneLayoutInfoMapper
{
    public static LocationLayoutWire ToWire(this SceneLayoutInfo layout) =>
        new(
            layout.Size.ToWire(),
            layout.Props.Select(prop => prop.ToWire()).ToArray(),
            layout.Buildings.Select(building => building.ToWire()).ToArray(),
            layout.Connectors.Select(connector => connector.ToWire()).ToArray(),
            layout.Creatures.Select(creature => creature.ToWire()).ToArray()
        );
}
