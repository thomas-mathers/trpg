using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Navigation;

internal static class DistrictNavigationGrid
{
    public static NavigationGrid Build(Location district, IEnumerable<Building> buildings)
    {
        var obstacles = buildings
            .Select(building =>
                (
                    Placement: new Placement(building.X, building.Y, building.Angle),
                    Footprint: new Footprint(building.Width, building.Depth)
                )
            )
            .ToArray();

        return new NavigationGrid(
            district.Width,
            district.Depth,
            CityLattice.CellSize,
            point =>
                obstacles.Any(obstacle =>
                    CityLattice.IsWithinBuildingClearance(
                        obstacle.Placement,
                        obstacle.Footprint,
                        point
                    )
                )
        );
    }
}
