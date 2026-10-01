using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class PropFootprintCatalogTests
{
    [Fact]
    public void Get_ReturnsPositiveFootprintsAndNonNegativeClearance_ForEveryKey()
    {
        // Act
        var specs = PropFootprintCatalog.Keys.Select(PropFootprintCatalog.Get).ToArray();

        // Assert
        Assert.All(
            specs,
            spec =>
            {
                Assert.True(spec.Width > 0);
                Assert.True(spec.Depth > 0);
                Assert.True(spec.FrontClearance >= 0);
            }
        );
    }

    [Fact]
    public void Get_Throws_WhenTheKeyIsNotCataloged()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => PropFootprintCatalog.Get("prop.unknown"));
    }
}
