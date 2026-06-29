using TRPG.Models;

namespace TRPG.Algorithms;

internal static class CityLayout {
    private const int StreetWidth = 2;

    private static readonly Dictionary<BuildingType, (int Width, int Height)> Sizes = new() {
        [BuildingType.House] = (4, 4),
        [BuildingType.ArcaneShop] = (5, 4),
        [BuildingType.Apothecary] = (5, 4),
        [BuildingType.Bakery] = (5, 4),
        [BuildingType.Blacksmith] = (5, 4),
        [BuildingType.Jail] = (5, 4),
        [BuildingType.GeneralGoods] = (5, 5),
        [BuildingType.Stable] = (5, 5),
        [BuildingType.GuildHall] = (6, 5),
        [BuildingType.Library] = (6, 5),
        [BuildingType.Tavern] = (6, 5),
        [BuildingType.Temple] = (7, 6),
        [BuildingType.Castle] = (8, 8)
    };

    public static void PlaceBuildings(City city, IList<Building> buildings) {
        var sorted = buildings
            .OrderByDescending(b => Sizes[b.BuildingType].Height)
            .ThenByDescending(b => Sizes[b.BuildingType].Width)
            .ToList();

        var x = 0;
        var y = 0;
        var rowHeight = 0;

        foreach (var building in sorted) {
            var (w, h) = Sizes[building.BuildingType];

            if (x + w > city.Width) {
                x = 0;
                y += rowHeight + StreetWidth;
                rowHeight = 0;
            }

            building.Boundary = new Rectangle(x, y, x + w, y + h);
            x += w + StreetWidth;
            rowHeight = Math.Max(rowHeight, h);
        }
    }
}