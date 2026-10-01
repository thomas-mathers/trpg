using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DistrictLayoutGeneratorTests
{
    private static readonly Footprint SeatFootprint = new(Width: 1.5, Depth: 0.5);

    private static DistrictBuildingInput[] Buildings(int count) =>
        Enumerable
            .Range(0, count)
            .Select(index => new DistrictBuildingInput(
                Guid.NewGuid(),
                new Footprint(Width: 8 + index % 5 * 2, Depth: 6 + index % 3 * 2)
            ))
            .ToArray();

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

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(25)]
    public void Generate_PlacesEveryBuildingInsideTheDistrict(int buildingCount)
    {
        // Arrange
        var inputs = Buildings(buildingCount);

        // Act
        var layout = DistrictLayoutGenerator.Generate(inputs, Seats(3));

        // Assert
        Assert.Equal(buildingCount, layout.Buildings.Count);
        Assert.All(
            layout.Buildings,
            building =>
                Assert.True(
                    BoxOf(building, inputs).IsInside(layout.District.Width, layout.District.Depth)
                )
        );
    }

    [Fact]
    public void Generate_PlacesNoTwoBuildingsOverlapping()
    {
        // Arrange
        var inputs = Buildings(25);

        // Act
        var layout = DistrictLayoutGenerator.Generate(inputs, Seats(3));

        // Assert
        var boxes = layout.Buildings.Select(building => BoxOf(building, inputs)).ToArray();
        var overlapping = boxes.SelectMany(
            (box, index) => boxes.Skip(index + 1).Where(box.Overlaps)
        );
        Assert.Empty(overlapping);
    }

    [Fact]
    public void Generate_KeepsABuildingGapBetweenNeighbors()
    {
        // Arrange
        var inputs = Buildings(12);

        // Act
        var layout = DistrictLayoutGenerator.Generate(inputs, []);

        // Assert
        var boxes = layout
            .Buildings.Select(building =>
                BoxOf(building, inputs).Inflated(LocationSizer.BuildingGap / 2 - 0.01)
            )
            .ToArray();
        var overlapping = boxes.SelectMany(
            (box, index) => boxes.Skip(index + 1).Where(box.Overlaps)
        );
        Assert.Empty(overlapping);
    }

    [Fact]
    public void Generate_PlacesDoorsOnTheStreetEdge()
    {
        // Arrange
        var inputs = Buildings(10);

        // Act
        var layout = DistrictLayoutGenerator.Generate(inputs, []);

        // Assert
        var streetCenterY = layout.District.Depth / 2;
        var halfStreet = LocationSizer.StreetWidth / 2;
        Assert.All(
            layout.Buildings,
            building =>
                Assert.Equal(
                    halfStreet,
                    Math.Abs(building.DoorPoint.Y - streetCenterY),
                    precision: 6
                )
        );
    }

    [Fact]
    public void Generate_PutsBuildingsOnBothSidesOfTheStreet()
    {
        // Act
        var layout = DistrictLayoutGenerator.Generate(Buildings(10), []);

        // Assert
        var streetCenterY = layout.District.Depth / 2;
        Assert.Contains(layout.Buildings, building => building.Placement.Y < streetCenterY);
        Assert.Contains(layout.Buildings, building => building.Placement.Y > streetCenterY);
    }

    [Fact]
    public void Generate_PlacesSeatsInsideTheStreetFacingIt()
    {
        // Arrange
        var inputs = Buildings(8);

        // Act
        var layout = DistrictLayoutGenerator.Generate(inputs, Seats(3));

        // Assert
        var streetCenterY = layout.District.Depth / 2;
        Assert.Equal(3, layout.Seats.Count);
        Assert.All(
            layout.Seats,
            seat =>
            {
                var box = OrientedBox.From(seat.Placement, SeatFootprint);
                var facesSouth = Math.Abs(seat.Placement.Angle - Math.PI) < 1e-9;
                Assert.True(box.IsInside(layout.District.Width, layout.District.Depth));
                Assert.Equal(seat.Placement.Y < streetCenterY, facesSouth);
                Assert.True(
                    Math.Abs(seat.Placement.Y - streetCenterY) < LocationSizer.StreetWidth / 2
                );
            }
        );
        var seatBoxes = layout.Seats.Select(seat =>
            OrientedBox.From(seat.Placement, SeatFootprint)
        );
        var buildingBoxes = layout.Buildings.Select(building => BoxOf(building, inputs)).ToArray();
        Assert.DoesNotContain(seatBoxes, seatBox => buildingBoxes.Any(seatBox.Overlaps));
    }

    [Fact]
    public void Generate_ReturnsTheSameLayout_ForTheSameInput()
    {
        // Arrange
        var inputs = Buildings(9);
        var seats = Seats(3);

        // Act
        var first = DistrictLayoutGenerator.Generate(inputs, seats);
        var second = DistrictLayoutGenerator.Generate(inputs, seats);

        // Assert
        Assert.Equal(first.District, second.District);
        Assert.Equal(first.Buildings, second.Buildings);
        Assert.Equal(first.Seats, second.Seats);
    }

    [Fact]
    public void Generate_ReturnsADistrictAtLeastAsLargeAsTheSizedMinimum()
    {
        // Arrange
        var inputs = Buildings(10);
        var sized = LocationSizer.SizeDistrict(
            inputs.Select(input => input.Footprint).ToArray(),
            3 * SeatFootprint.Width * SeatFootprint.Depth
        );

        // Act
        var layout = DistrictLayoutGenerator.Generate(inputs, Seats(3));

        // Assert
        Assert.True(layout.District.Width >= sized.Width);
        Assert.True(layout.District.Depth >= sized.Depth);
    }
}
