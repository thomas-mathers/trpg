using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class WildernessCatalogTests
{
    [Fact]
    public void Size_Is300By300Meters()
    {
        // Act
        var size = WildernessCatalog.Size;

        // Assert
        Assert.Equal(300, size.Width);
        Assert.Equal(300, size.Depth);
    }
}
