using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DistrictGridTests
{
    private static readonly DistrictType[] AllTypes =
    [
        DistrictType.Residential,
        DistrictType.CityCenter,
        DistrictType.CityEntrance,
        DistrictType.Encampment,
        DistrictType.Governmental,
        DistrictType.HolySite,
        DistrictType.Scientific,
    ];

    private readonly Guid _cityId = Guid.NewGuid();

    [Fact]
    public void Assign_PutsTheCenterAtTheOriginAndTheEntranceDirectlySouth()
    {
        // Arrange
        var districts = Districts(AllTypes);

        // Act
        var cells = DistrictGrid.Assign(districts);

        // Assert
        Assert.Equal(new GridCell(0, 0), cells[Of(districts, DistrictType.CityCenter)]);
        Assert.Equal(new GridCell(0, 1), cells[Of(districts, DistrictType.CityEntrance)]);
    }

    [Fact]
    public void Assign_GivesEveryDistrictItsOwnCell()
    {
        // Arrange
        var districts = Districts(AllTypes);

        // Act
        var cells = DistrictGrid.Assign(districts);

        // Assert
        Assert.Equal(districts.Count, cells.Values.Distinct().Count());
    }

    [Fact]
    public void Assign_TouchesEveryCellToAnotherEdgeWise()
    {
        // Arrange
        var districts = Districts(AllTypes);

        // Act
        var cells = DistrictGrid.Assign(districts).Values.ToHashSet();

        // Assert
        Assert.All(
            cells,
            cell => Assert.Contains(DistrictGrid.Edges, edge => cells.Contains(cell.Step(edge)))
        );
    }

    [Fact]
    public void Assign_LeavesTheCellSouthOfTheEntranceFree()
    {
        // Arrange
        var districts = Districts(AllTypes);

        // Act
        var cells = DistrictGrid.Assign(districts);

        // Assert
        Assert.DoesNotContain(new GridCell(0, 2), cells.Values);
    }

    private IReadOnlyList<District> Districts(params DistrictType[] types) =>
        types.Select(type => Builders.MakeDistrict(_cityId, type)).ToArray();

    private static Guid Of(IReadOnlyList<District> districts, DistrictType type) =>
        districts.Single(district => district.DistrictType == type).LocationId;
}
