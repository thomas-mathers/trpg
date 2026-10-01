using TRPG.Application.Scenes.Results;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Mappers;

internal static class PropMapper
{
    public static ScenePropLayout ToLayout(this Prop prop) =>
        new(
            prop.Id,
            PropModelResolver.Resolve(prop),
            new Placement(prop.X, prop.Y, prop.Angle),
            new Footprint(prop.Width, prop.Depth)
        );
}
