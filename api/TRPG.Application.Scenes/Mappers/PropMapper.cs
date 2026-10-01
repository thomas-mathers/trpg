using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Mappers;

internal static class PropMapper
{
    public static SceneBoxLayout ToLayout(this Prop prop) =>
        new(
            prop.Id,
            new Placement(prop.X, prop.Y, prop.Angle),
            new Footprint(prop.Width, prop.Depth)
        );
}
