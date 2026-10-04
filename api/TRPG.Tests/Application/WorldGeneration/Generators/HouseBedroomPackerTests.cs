using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class HouseBedroomPackerTests
{
    [Fact]
    public void Pack_KeepsTheGroupsAsIs_WhenThereAreEnoughBedrooms()
    {
        // Arrange
        var groups = MakeGroups(2, 1, 1);

        // Act
        var result = HouseBedroomPacker.Pack(groups);

        // Assert
        Assert.Equal(groups, result);
    }

    [Fact]
    public void Pack_DoublesChildrenUp_WhenTheLargestHouseRunsOutOfBedrooms()
    {
        // Arrange
        var groups = MakeGroups(2, 1, 1, 1, 1);

        // Act
        var result = HouseBedroomPacker.Pack(groups);

        // Assert
        Assert.Equal(HouseBedroomPacker.MaximumBedrooms, result.Count);
        Assert.Equal(
            groups.SelectMany(group => group).Order(),
            result.SelectMany(group => group).Order()
        );
        Assert.All(
            result,
            group => Assert.InRange(group.Count, 1, HouseBedroomPacker.BedsPerBedroom)
        );
    }

    [Fact]
    public void Pack_KeepsParentsTogether_WhenChildrenDoubleUp()
    {
        // Arrange
        var groups = MakeGroups(2, 1, 1, 1, 1);

        // Act
        var result = HouseBedroomPacker.Pack(groups);

        // Assert
        Assert.Equal(groups[0], result[0]);
    }

    [Fact]
    public void Pack_Throws_WhenTheHouseholdExceedsTheLargestHouse()
    {
        // Arrange
        var groups = MakeGroups([
            2,
            .. Enumerable.Repeat(1, HouseBedroomPacker.MaximumHouseholdSize - 1),
        ]);

        // Act
        var act = () => HouseBedroomPacker.Pack(groups);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Pack_Throws_WhenAGroupExceedsTheBedsPerBedroom()
    {
        // Arrange
        var groups = MakeGroups(3);

        // Act
        var act = () => HouseBedroomPacker.Pack(groups);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    private static IReadOnlyList<IReadOnlyList<Guid>> MakeGroups(params int[] sizes) =>
        sizes
            .Select(size =>
                (IReadOnlyList<Guid>)Enumerable.Range(0, size).Select(_ => Guid.NewGuid()).ToArray()
            )
            .ToArray();
}
