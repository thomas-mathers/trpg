using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class BuildingTemplateCatalog
{
    private static readonly IReadOnlyDictionary<
        BuildingType,
        IReadOnlyList<BuildingTemplate>
    > ByBuildingType = new Dictionary<BuildingType, IReadOnlyList<BuildingTemplate>>
    {
        [BuildingType.House] =
        [
            Template("Small", 8, 10, Open("Living Room"), Single("Bedroom 1", 3, 5.25)),
            Template(
                "Medium",
                10,
                12,
                Open("Living Room"),
                Wing(2.25, 12, Sized("Bedroom 1", 3.75, 4), Sized("Bedroom 2", 3.75, 4))
            ),
            Template(
                "Large",
                12,
                14,
                Open("Living Room"),
                Wing(
                    2.5,
                    14,
                    Sized("Bedroom 1", 4.25, 3.5),
                    Sized("Bedroom 2", 4.25, 3.5),
                    Sized("Bedroom 3", 4.25, 3.5)
                )
            ),
        ],
        [BuildingType.GeneralGoods] = Standard(11, 15, "Shop", Sized("Living Quarters", 4.25, 5.5)),
        [BuildingType.Bakery] = Standard(12, 14, "Bakery", Sized("Living Quarters", 4.75, 5)),
        [BuildingType.Tavern] = Standard(
            20,
            18,
            "Common Room",
            Sized("Owner's Quarters", 5.25, 4.5)
        ),
        [BuildingType.Inn] =
        [
            Template(
                "Standard",
                20,
                24,
                Open("Lobby"),
                Wing(
                    2.5,
                    24,
                    Sized("North Guest Room", 4.25, 3.5),
                    Sized("South Guest Room", 4.25, 3.5),
                    Sized("East Guest Room", 4.25, 3.5),
                    Sized("West Guest Room", 4.25, 3.5)
                ),
                Single("Owner's Quarters", 5.25, 4.5)
            ),
        ],
        [BuildingType.Tailor] = Standard(9, 14, "Shop", Sized("Living Quarters", 3.5, 6.75)),
        [BuildingType.Carpenter] = Standard(
            16,
            20,
            "Workshop",
            Sized("Living Quarters", 5.25, 4.5)
        ),
        [BuildingType.Jeweler] = Standard(9, 12, "Shop", Sized("Living Quarters", 3.5, 6.75)),
        [BuildingType.GuildHall] =
        [
            Template(
                "Standard",
                25,
                22,
                Open("Hall"),
                Wing(
                    2.5,
                    22,
                    Sized("Guild Master's Chamber", 4.25, 5.25),
                    Sized("Member Room 1", 4.25, 3.5),
                    Sized("Member Room 2", 4.25, 3.5),
                    Sized("Member Room 3", 4.25, 3.5),
                    Sized("Member Room 4", 4.25, 3.5),
                    Sized("Member Room 5", 4.25, 3.5)
                )
            ),
        ],
        [BuildingType.Library] =
        [
            Template("Standard", 24, 26, Open("Reading Room"), Open("Study")),
        ],
        [BuildingType.ArcaneShop] = Standard(12, 16, "Shop", Sized("Living Quarters", 4.75, 5)),
        [BuildingType.Apothecary] = Standard(12, 15, "Shop", Sized("Living Quarters", 4.75, 5)),
        [BuildingType.Castle] = Standard(38, 32, "Great Hall", Sized("Royal Chambers", 7, 5.75)),
        [BuildingType.Jail] =
        [
            Template("Standard", 17, 22, Open("Guard Station"), Open(JailRoomNames.Cells)),
        ],
        [BuildingType.Temple] = Standard(26, 36, "Sanctuary", Sized("Quarters", 4.25, 3.5)),
        [BuildingType.Barracks] =
        [
            Template(
                "Standard",
                30,
                22,
                Open("Drill Hall"),
                Wing(
                    2.5,
                    22,
                    Sized("Officer's Quarters", 5.25, 4.5),
                    Sized("Barracks Dormitory", 5.25, 7.25)
                )
            ),
        ],
        [BuildingType.Blacksmith] = Standard(
            17,
            20,
            "Workshop",
            Sized("Living Quarters", 5.25, 4.5)
        ),
        [BuildingType.Stable] = Standard(28, 13, "Stable", Sized("Living Quarters", 5.25, 4.5)),
    };

    internal static IReadOnlyCollection<BuildingType> BuildingTypes =>
        ByBuildingType.Keys.ToArray();

    internal static IReadOnlyList<BuildingTemplate> GetTemplates(BuildingType buildingType) =>
        ByBuildingType[buildingType];

    internal static BuildingTemplate Resolve(
        BuildingType buildingType,
        IReadOnlyCollection<Room> rooms
    )
    {
        var placedRooms = rooms.Where(room => !LocationLayoutContext.IsHallway(room)).ToArray();

        return ByBuildingType[buildingType].FirstOrDefault(template => template.Covers(placedRooms))
            ?? throw new InvalidOperationException(
                $"No {buildingType} template has a room for each of: {string.Join(", ", placedRooms.Select(room => room.Name))}."
            );
    }

    private static BuildingTemplate[] Standard(
        double width,
        double depth,
        string groundRoomName,
        TemplateRoom upperRoom
    ) => [Template("Standard", width, depth, Open(groundRoomName), Single(upperRoom))];

    private static BuildingTemplate Template(
        string name,
        double width,
        double depth,
        params TemplateFloor[] floors
    ) => new(name, new Footprint(width, depth), floors);

    private static TemplateFloor Open(string name) => new([new TemplateRoom(name)]);

    private static TemplateFloor Single(string name, double width, double depth) =>
        Single(Sized(name, width, depth));

    private static TemplateFloor Single(TemplateRoom room) => new([room]);

    private static TemplateFloor Wing(
        double hallwayWidth,
        double depth,
        params TemplateRoom[] rooms
    ) => new(rooms, new Footprint(hallwayWidth, depth));

    private static TemplateRoom Sized(string name, double width, double depth) =>
        new(name, new Footprint(width, depth));
}
