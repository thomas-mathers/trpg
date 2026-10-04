using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DistrictLayoutGeneratorTests
{
    private static readonly Footprint SeatFootprint = new(Width: 1.5, Depth: 0.5);

    private static readonly IReadOnlyDictionary<DistrictType, BuildingType[]> Rosters =
        new Dictionary<DistrictType, BuildingType[]>
        {
            [DistrictType.CityCenter] =
            [
                BuildingType.GuildHall,
                BuildingType.Inn,
                BuildingType.Tavern,
                BuildingType.GeneralGoods,
                BuildingType.Bakery,
                BuildingType.Tailor,
                BuildingType.Carpenter,
                BuildingType.Jeweler,
            ],
            [DistrictType.Encampment] =
            [
                BuildingType.Barracks,
                BuildingType.Blacksmith,
                BuildingType.Stable,
            ],
            [DistrictType.Scientific] =
            [
                BuildingType.Library,
                BuildingType.ArcaneShop,
                BuildingType.Apothecary,
            ],
            [DistrictType.Governmental] = [BuildingType.Castle, BuildingType.Jail],
            [DistrictType.HolySite] = [BuildingType.Temple],
            [DistrictType.CityEntrance] = [BuildingType.Inn, BuildingType.Stable],
            [DistrictType.Residential] = [BuildingType.House],
        };

    public static TheoryData<DistrictType> AllDistrictTypes => new(Enum.GetValues<DistrictType>());

    private static Footprint FootprintOf(BuildingType type, int index) =>
        type switch
        {
            BuildingType.Castle => new Footprint(Width: 38, Depth: 32),
            BuildingType.GuildHall or BuildingType.Inn => new Footprint(Width: 20, Depth: 24),
            BuildingType.House => (index % 3) switch
            {
                0 => new Footprint(Width: 8, Depth: 10),
                1 => new Footprint(Width: 10, Depth: 12),
                _ => new Footprint(Width: 12, Depth: 14),
            },
            _ => new Footprint(Width: 12 + (index % 3 * 2), Depth: 14),
        };

    private static DistrictBuildingInput[] Buildings(DistrictType type, int count)
    {
        var roster = Rosters[type];

        return Enumerable
            .Range(0, count)
            .Select(index =>
            {
                var buildingType = roster[index % roster.Length];

                return new DistrictBuildingInput(
                    Guid.NewGuid(),
                    buildingType,
                    FootprintOf(buildingType, index)
                );
            })
            .ToArray();
    }

    private static DistrictBuildingInput[] RosterOnce(DistrictType type) =>
        Buildings(type, Rosters[type].Length);

    private static DistrictSeatInput[] Seats(int count) =>
        Enumerable
            .Range(0, count)
            .Select(_ => new DistrictSeatInput(Guid.NewGuid(), SeatFootprint))
            .ToArray();

    private static OrientedBox BoxOf(
        DistrictBuildingLayout building,
        IReadOnlyCollection<DistrictBuildingInput> inputs
    ) =>
        OrientedBox.From(
            building.Placement,
            inputs.Single(input => input.Id == building.Id).Footprint
        );

    private static OrientedBox[] BuildingBoxes(
        DistrictLayout layout,
        IReadOnlyCollection<DistrictBuildingInput> inputs
    ) => layout.Buildings.Select(building => BoxOf(building, inputs)).ToArray();

    private static OrientedBox[] FurnishingBoxes(DistrictLayout layout) =>
        layout
            .Seats.Select(seat => OrientedBox.From(seat.Placement, SeatFootprint))
            .Concat(layout.Decor.Select(item => OrientedBox.From(item.Placement, item.Footprint)))
            .ToArray();

    [Theory]
    [MemberData(nameof(AllDistrictTypes))]
    public void Generate_PlacesEveryBuildingInsideTheDistrict(DistrictType type)
    {
        // Arrange
        var inputs = RosterOnce(type);

        // Act
        var layout = DistrictLayoutGenerator.Generate(type, inputs, Seats(3), seed: 1);

        // Assert
        Assert.Equal(inputs.Length, layout.Buildings.Count);
        Assert.All(
            BuildingBoxes(layout, inputs),
            box => Assert.True(box.IsInside(layout.District.Width, layout.District.Depth))
        );
    }

    [Theory]
    [InlineData(DistrictType.Residential, 1)]
    [InlineData(DistrictType.Residential, 7)]
    [InlineData(DistrictType.Residential, 25)]
    [InlineData(DistrictType.Residential, 80)]
    [InlineData(DistrictType.CityCenter, 12)]
    [InlineData(DistrictType.CityEntrance, 6)]
    public void Generate_PlacesEveryBuildingWithoutOverlap_AcrossSeeds(DistrictType type, int count)
    {
        for (var seed = 0; seed < 25; seed++)
        {
            // Arrange
            var inputs = Buildings(type, count);

            // Act
            var layout = DistrictLayoutGenerator.Generate(type, inputs, Seats(3), seed);

            // Assert
            var boxes = BuildingBoxes(layout, inputs);
            Assert.Equal(count, boxes.Length);
            Assert.All(
                boxes,
                box => Assert.True(box.IsInside(layout.District.Width, layout.District.Depth))
            );
            Assert.All(
                boxes.SelectMany(first => boxes.Where(second => second != first), (a, b) => (a, b)),
                pair => Assert.False(pair.a.Overlaps(pair.b))
            );
        }
    }

    [Theory]
    [MemberData(nameof(AllDistrictTypes))]
    public void Generate_KeepsBuildingsOffTheStreetsAndSquare(DistrictType type)
    {
        for (var seed = 0; seed < 25; seed++)
        {
            // Arrange
            var inputs = Buildings(type, Rosters[type].Length * 3);

            // Act
            var layout = DistrictLayoutGenerator.Generate(type, inputs, Seats(3), seed);

            // Assert
            var open = layout.Plan.Streets.Append(layout.Plan.Square).Select(rect => rect.ToBox());
            Assert.All(
                BuildingBoxes(layout, inputs),
                building => Assert.All(open, area => Assert.False(building.Overlaps(area)))
            );
        }
    }

    [Theory]
    [MemberData(nameof(AllDistrictTypes))]
    public void Generate_PutsEveryDoorOnAStreetOrCourt(DistrictType type)
    {
        for (var seed = 0; seed < 25; seed++)
        {
            // Arrange
            var inputs = Buildings(type, Rosters[type].Length * 2);

            // Act
            var layout = DistrictLayoutGenerator.Generate(type, inputs, Seats(3), seed);

            // Assert
            var open = layout.Plan.Streets.Concat(layout.Plan.Courts).Append(layout.Plan.Square);
            Assert.All(
                layout.Buildings,
                building => Assert.Contains(open, rect => rect.Contains(building.DoorPoint))
            );
        }
    }

    [Theory]
    [MemberData(nameof(AllDistrictTypes))]
    public void Generate_KeepsFurnishingInsideTheDistrictAndClearOfBuildings(DistrictType type)
    {
        for (var seed = 0; seed < 25; seed++)
        {
            // Arrange
            var inputs = Buildings(type, Rosters[type].Length * 2);

            // Act
            var layout = DistrictLayoutGenerator.Generate(type, inputs, Seats(3), seed);

            // Assert
            var buildings = BuildingBoxes(layout, inputs);
            Assert.All(
                FurnishingBoxes(layout),
                box =>
                {
                    Assert.True(box.IsInside(layout.District.Width, layout.District.Depth));
                    Assert.All(buildings, building => Assert.False(box.Overlaps(building)));
                }
            );
        }
    }

    [Theory]
    [MemberData(nameof(AllDistrictTypes))]
    public void Generate_KeepsDoorApproachesClearOfFurnishing(DistrictType type)
    {
        for (var seed = 0; seed < 25; seed++)
        {
            // Arrange
            var inputs = Buildings(type, Rosters[type].Length * 2);

            // Act
            var layout = DistrictLayoutGenerator.Generate(type, inputs, Seats(3), seed);

            // Assert
            var approaches = layout.Buildings.Select(ApproachBox).ToArray();
            Assert.All(
                FurnishingBoxes(layout),
                box => Assert.All(approaches, approach => Assert.False(box.Overlaps(approach)))
            );
        }
    }

    [Theory]
    [MemberData(nameof(AllDistrictTypes))]
    public void Generate_PlacesEverySeatWithoutOverlap(DistrictType type)
    {
        // Arrange
        var inputs = RosterOnce(type);
        var seats = Seats(3);

        // Act
        var layout = DistrictLayoutGenerator.Generate(type, inputs, seats, seed: 4);

        // Assert
        var furnishing = FurnishingBoxes(layout);
        Assert.Equal(seats.Length, layout.Seats.Count);
        Assert.All(
            furnishing.Select((box, index) => (box, index)),
            item =>
                Assert.All(
                    furnishing.Where((_, other) => other != item.index),
                    other => Assert.False(item.box.Overlaps(other))
                )
        );
    }

    [Fact]
    public void Generate_BacksEverySeatAgainstABuildingFront_InTheCityCenter()
    {
        // Arrange
        var inputs = RosterOnce(DistrictType.CityCenter);

        // Act
        var layout = DistrictLayoutGenerator.Generate(
            DistrictType.CityCenter,
            inputs,
            Seats(3),
            seed: 4
        );

        // Assert
        var buildings = BuildingBoxes(layout, inputs);
        Assert.All(
            layout.Seats,
            seat =>
            {
                var (sin, cos) = Math.SinCos(seat.Placement.Angle);
                var reach = SeatFootprint.Depth / 2 + 1.5;
                var ends = new[] { -1, 1 }.Select(side => new OrientedBox(
                    seat.Placement.X - reach * sin + side * SeatFootprint.Width / 2 * cos,
                    seat.Placement.Y + reach * cos + side * SeatFootprint.Width / 2 * sin,
                    0.05,
                    0.05,
                    0
                ));
                Assert.All(
                    ends,
                    end => Assert.Contains(buildings, building => building.Overlaps(end))
                );
            }
        );
    }

    [Fact]
    public void Generate_ReturnsTheSameLayout_WhenTheSeedMatches()
    {
        // Arrange
        var inputs = Buildings(DistrictType.Residential, 20);

        // Act
        var first = DistrictLayoutGenerator.Generate(DistrictType.Residential, inputs, [], 9);
        var second = DistrictLayoutGenerator.Generate(DistrictType.Residential, inputs, [], 9);

        // Assert
        Assert.Equal(first.Buildings, second.Buildings);
        Assert.Equal(first.District, second.District);
    }

    [Fact]
    public void Generate_ReturnsADifferentLayout_WhenTheSeedDiffers()
    {
        // Arrange
        var inputs = Buildings(DistrictType.Residential, 20);

        // Act
        var first = DistrictLayoutGenerator.Generate(DistrictType.Residential, inputs, [], 1);
        var second = DistrictLayoutGenerator.Generate(DistrictType.Residential, inputs, [], 2);

        // Assert
        Assert.NotEqual(first.Buildings, second.Buildings);
    }

    [Theory]
    [InlineData(DistrictType.Residential, PropModel.FurnitureWell)]
    [InlineData(DistrictType.CityCenter, PropModel.FurnitureFountain)]
    [InlineData(DistrictType.Encampment, PropModel.FurnitureFirePit)]
    [InlineData(DistrictType.Scientific, PropModel.FurnitureStatue)]
    [InlineData(DistrictType.Governmental, PropModel.FurnitureMonument)]
    [InlineData(DistrictType.HolySite, PropModel.FurnitureShrine)]
    [InlineData(DistrictType.CityEntrance, PropModel.FurnitureWaystone)]
    public void Generate_PlacesTheDistrictCenterpiece(DistrictType type, PropModel expected)
    {
        // Arrange
        var inputs = RosterOnce(type);

        // Act
        var layout = DistrictLayoutGenerator.Generate(type, inputs, Seats(3), seed: 3);

        // Assert
        Assert.Contains(layout.Decor, item => item.Model == expected);
    }

    [Fact]
    public void Generate_PutsAtMostTwoBuildingsOnTheNorthRow_OfACityCenter()
    {
        // Arrange
        var inputs = RosterOnce(DistrictType.CityCenter);

        // Act
        var layout = DistrictLayoutGenerator.Generate(DistrictType.CityCenter, inputs, [], 3);

        // Assert
        Assert.Equal(2, layout.Buildings.Count(building => building.Placement.Angle == Math.PI));
    }

    [Theory]
    [InlineData(PropModel.ContainerCrate)]
    [InlineData(PropModel.ContainerBarrel)]
    public void Generate_StacksStorageBesideShopDoors_InTheCityCenter(PropModel expected)
    {
        // Arrange
        var inputs = RosterOnce(DistrictType.CityCenter);

        // Act
        var layout = DistrictLayoutGenerator.Generate(DistrictType.CityCenter, inputs, [], 3);

        // Assert
        Assert.Contains(layout.Decor, item => item.Model == expected);
    }

    [Fact]
    public void Generate_PlacesANoticeBoardInTheSquare()
    {
        // Arrange
        var inputs = RosterOnce(DistrictType.CityCenter);

        // Act
        var layout = DistrictLayoutGenerator.Generate(DistrictType.CityCenter, inputs, [], 3);

        // Assert
        Assert.Contains(layout.Decor, item => item.Model == PropModel.FurnitureNoticeBoard);
    }

    [Theory]
    [InlineData(DistrictType.CityCenter)]
    [InlineData(DistrictType.Residential)]
    public void Generate_AcceptsAnyBuildingMix_WhenTheDistrictKindDoesNotExpectIt(DistrictType type)
    {
        // Arrange
        var inputs = new[]
        {
            new DistrictBuildingInput(
                Guid.NewGuid(),
                BuildingType.Castle,
                FootprintOf(BuildingType.Castle, 0)
            ),
            new DistrictBuildingInput(
                Guid.NewGuid(),
                BuildingType.Library,
                FootprintOf(BuildingType.Library, 1)
            ),
            new DistrictBuildingInput(
                Guid.NewGuid(),
                BuildingType.Temple,
                FootprintOf(BuildingType.Temple, 2)
            ),
            new DistrictBuildingInput(
                Guid.NewGuid(),
                BuildingType.House,
                FootprintOf(BuildingType.House, 3)
            ),
        };

        // Act
        var layout = DistrictLayoutGenerator.Generate(type, inputs, Seats(3), seed: 5);

        // Assert
        Assert.Equal(inputs.Length, layout.Buildings.Count);
    }

    [Fact]
    public void Generate_ReturnsAnEmptyLayoutWithFurnishing_WhenThereAreNoBuildings()
    {
        // Act
        var layout = DistrictLayoutGenerator.Generate(DistrictType.CityEntrance, [], Seats(3), 1);

        // Assert
        Assert.Empty(layout.Buildings);
        Assert.Equal(3, layout.Seats.Count);
    }

    [Theory]
    [InlineData(0, 0, 0, 0.0, -5)]
    [InlineData(10, 20, 2, 10.0, 25)]
    public void FrontDoorPoint_SitsHalfADepthAheadOfTheBuilding(
        double x,
        double y,
        int facing,
        double expectedX,
        double expectedY
    )
    {
        // Arrange
        var placement = new Placement(x, y, facing * Math.PI / 2);

        // Act
        var door = DistrictLayoutGenerator.FrontDoorPoint(placement, new Footprint(8, 10));

        // Assert
        Assert.Equal(expectedX, door.X, precision: 6);
        Assert.Equal(expectedY, door.Y, precision: 6);
    }

    private static OrientedBox ApproachBox(DistrictBuildingLayout layout)
    {
        var angle = layout.Placement.Angle;
        var center = new Placement(
            layout.DoorPoint.X + 0.75 * Math.Sin(angle),
            layout.DoorPoint.Y - 0.75 * Math.Cos(angle),
            angle
        );

        return OrientedBox.From(center, new Footprint(Width: 2, Depth: 1.5));
    }
}
