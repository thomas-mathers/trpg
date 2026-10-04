using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class PropModelNamesTests
{
    [Theory]
    [InlineData(PropModel.FurnitureDisplayShelf, "Display Shelf")]
    [InlineData(PropModel.FurnitureRug, "Rug")]
    [InlineData(PropModel.FurnitureFireplace, "Fireplace")]
    public void DisplayName_SplitsPascalCaseWords_AndDropsTheFurniturePrefix(
        PropModel model,
        string expected
    )
    {
        // Arrange

        // Act
        var name = PropModelNames.DisplayName(model);

        // Assert
        Assert.Equal(expected, name);
    }
}
