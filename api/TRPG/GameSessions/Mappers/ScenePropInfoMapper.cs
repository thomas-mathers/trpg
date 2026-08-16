using TRPG.Application.Scenes.Queries;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class ScenePropInfoMapper
{
    public static NearbyPropSnapshot ToSnapshot(this ScenePropInfo prop) =>
        new(prop.Id, prop.Name, prop.Type);
}
