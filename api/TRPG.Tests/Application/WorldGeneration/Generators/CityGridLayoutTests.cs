using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class CityGridLayoutTests
{
    private const double Half = CityGrid.CellSize / 2;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Generate_SizesDistrictBuildingsInWholeCells(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var oddlySized = DistrictBuildings(world)
            .Where(building =>
                !IsMultipleOf(building.Width, CityGrid.CellSize)
                || !IsMultipleOf(building.Depth, CityGrid.CellSize)
            )
            .Select(building => $"{building.BuildingType}: {building.Width} x {building.Depth}")
            .ToList();
        Assert.True(oddlySized.Count == 0, string.Join(Environment.NewLine, oddlySized));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Generate_AlignsDistrictBuildingsToCellBoundaries(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var misaligned = DistrictBuildings(world)
            .Select(building => (building, bounds: BoundsOf(building)))
            .Where(item =>
                !IsMultipleOf(item.bounds.Left, CityGrid.CellSize)
                || !IsMultipleOf(item.bounds.Top, CityGrid.CellSize)
            )
            .Select(item =>
                $"{item.building.BuildingType}: left {item.bounds.Left}, top {item.bounds.Top}"
            )
            .ToList();
        Assert.True(misaligned.Count == 0, string.Join(Environment.NewLine, misaligned));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Generate_SizesDistrictsInOddCellCounts(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var evenSided = Districts(world)
            .Where(district => !IsOddCellCount(district.Width) || !IsOddCellCount(district.Depth))
            .Select(district => $"{district.Width} x {district.Depth}")
            .ToList();
        Assert.True(evenSided.Count == 0, string.Join(Environment.NewLine, evenSided));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Generate_PlacesDistrictExitsOnAFacadeCellCentre(int iteration)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(iteration);
        var districtIds = Districts(world).Select(district => district.Id).ToHashSet();

        // Act
        var layout = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var offLattice = world
            .PlacedConnectors(layout)
            .Where(placed => districtIds.Contains(placed.Connector.OriginLocationId))
            .Where(placed => !IsFacadeCellCentre(placed.Exit.X, placed.Exit.Y))
            .Select(placed => $"({placed.Exit.X}, {placed.Exit.Y})")
            .ToList();
        Assert.True(offLattice.Count == 0, string.Join(Environment.NewLine, offLattice));
    }

    internal static bool IsMultipleOf(double value, double step) =>
        Math.Abs(value / step - Math.Round(value / step)) < 1e-6;

    internal static bool IsOddMultipleOf(double value, double step) =>
        IsMultipleOf(value, step) && Math.Round(value / step) % 2 != 0;

    private static bool IsOddCellCount(double length) =>
        IsMultipleOf(length, CityGrid.CellSize) && Math.Round(length / CityGrid.CellSize) % 2 != 0;

    private static bool IsFacadeCellCentre(double x, double y)
    {
        var xIsCentre = IsOddMultipleOf(x, Half);
        var yIsCentre = IsOddMultipleOf(y, Half);
        var xIsBoundary = IsMultipleOf(x, CityGrid.CellSize);
        var yIsBoundary = IsMultipleOf(y, CityGrid.CellSize);

        return (xIsCentre && yIsBoundary) || (yIsCentre && xIsBoundary);
    }

    private static List<Location> Districts(MiniLayoutWorldBuilder.MiniLayoutWorld world) =>
        world.Input.Locations.Where(location => location.Kind == LocationKind.District).ToList();

    private static List<Building> DistrictBuildings(MiniLayoutWorldBuilder.MiniLayoutWorld world)
    {
        var districtIds = Districts(world).Select(district => district.Id).ToHashSet();

        return world
            .Input.Buildings.Where(building => districtIds.Contains(building.ExteriorLocationId))
            .ToList();
    }

    private static Bounds BoundsOf(Building building)
    {
        var sideways = Math.Abs(Math.Round(building.Angle / (Math.PI / 2))) % 2 == 1;
        var width = sideways ? building.Depth : building.Width;
        var depth = sideways ? building.Width : building.Depth;

        return new Bounds(building.X - width / 2, building.Y - depth / 2);
    }

    private readonly record struct Bounds(double Left, double Top);
}
