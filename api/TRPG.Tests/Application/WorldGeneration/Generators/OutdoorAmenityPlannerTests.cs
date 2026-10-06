using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class OutdoorAmenityPlannerTests
{
    private static readonly Footprint Size = new(75, 75);
    private static readonly PlanRect Square = new(15, 15, 45, 45);

    [Theory]
    [InlineData(DistrictType.CityCenter, PropModel.FurnitureStall)]
    [InlineData(DistrictType.Residential, PropModel.FurnitureTree)]
    [InlineData(DistrictType.HolySite, PropModel.FurnitureFlowerBed)]
    [InlineData(DistrictType.Governmental, PropModel.FurnitureBanner)]
    [InlineData(DistrictType.Encampment, PropModel.FurnitureTrainingDummy)]
    [InlineData(DistrictType.CityEntrance, PropModel.FurnitureHitchingRail)]
    public void Place_UsesFunctionalZones(DistrictType type, PropModel expected)
    {
        var props = OutdoorAmenityPlanner.Place(type, Input());

        Assert.Contains(props, prop => prop.Model == expected);
    }

    [Fact]
    public void Place_ProvidesSittableBenchesAroundTheFountain()
    {
        // Arrange
        var fountain = new DistrictDecor(
            PropModel.FurnitureFountain,
            new Placement(37.5, 37.5, 0),
            PropFootprintCatalog.Get(PropModel.FurnitureFountain).Footprint
        );
        var input = Input() with { Existing = [fountain] };

        // Act
        var props = OutdoorAmenityPlanner.Place(DistrictType.CityCenter, input);

        // Assert
        Assert.Contains(props, prop => prop.Model == PropModel.SeatBench);
        Assert.DoesNotContain(props, prop => prop.Model == PropModel.FurnitureBench);
    }

    [Fact]
    public void Place_KeepsLanternsAndMarketStallsClearOfRoads()
    {
        var props = OutdoorAmenityPlanner.Place(DistrictType.CityCenter, Input());

        Assert.Contains(props, prop => prop.Model == PropModel.FurnitureStreetLantern);
        Assert.All(
            props,
            prop =>
            {
                var box = CityGrid.CellBox(prop.Placement, prop.Footprint);
                Assert.False(box.Overlaps(new OrientedBox(37.5, 37.5, 4.5, 75, 0)));
                Assert.False(box.Overlaps(new OrientedBox(37.5, 37.5, 75, 4.5, 0)));
            }
        );
    }

    [Fact]
    public void Place_OrientsMarketStallsTowardTheShopAisle()
    {
        var props = OutdoorAmenityPlanner.Place(DistrictType.CityCenter, Input());

        Assert.All(
            props.Where(prop => prop.Model == PropModel.FurnitureStall),
            prop => Assert.Equal(Math.PI / 2, prop.Placement.Angle)
        );
    }

    [Fact]
    public void Place_SpacesStreetLanternsAcrossRoadSegments()
    {
        // Arrange
        var input = Input();

        // Act
        var props = OutdoorAmenityPlanner.Place(DistrictType.CityCenter, input);

        // Assert
        var lanterns = props
            .Where(prop => prop.Model == PropModel.FurnitureStreetLantern)
            .ToArray();
        Assert.True(lanterns.Length > 1);
        foreach (var lantern in lanterns)
        {
            Assert.All(
                lanterns.Where(other => other != lantern),
                other =>
                {
                    var dx = lantern.Placement.X - other.Placement.X;
                    var dy = lantern.Placement.Y - other.Placement.Y;
                    Assert.True(dx * dx + dy * dy >= 24 * 24);
                }
            );
        }
    }

    private static DistrictPlan Plan() =>
        new(Size, [], [], [new PlanRect(3, 6, 12, 21), new PlanRect(60, 6, 12, 21)], Square, 37.5);

    private static OutdoorAmenityInput Input() =>
        new(Plan(), new Dictionary<Guid, BuildingType>(), [], [], Roads());

    private static RoadNetwork Roads()
    {
        var north = new RoadNode { X = 37.5, Y = 0 };
        var south = new RoadNode { X = 37.5, Y = 75 };
        var west = new RoadNode { X = 0, Y = 37.5 };
        var east = new RoadNode { X = 75, Y = 37.5 };
        return new RoadNetwork(
            [north, south, west, east],
            [
                new RoadEdge
                {
                    FromNodeId = north.Id,
                    ToNodeId = south.Id,
                    Class = RoadClass.Avenue,
                },
                new RoadEdge
                {
                    FromNodeId = west.Id,
                    ToNodeId = east.Id,
                    Class = RoadClass.Avenue,
                },
            ]
        );
    }
}
