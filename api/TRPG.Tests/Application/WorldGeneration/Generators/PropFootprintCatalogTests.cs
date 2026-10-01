using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class PropFootprintCatalogTests
{
    [Fact]
    public void Get_ReturnsPositiveFootprintsAndNonNegativeClearance_ForEveryModel()
    {
        // Act
        var specs = PropFootprintCatalog.Models.Select(PropFootprintCatalog.Get).ToArray();

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
    public void Models_CoversEveryPropModel()
    {
        // Act
        var models = PropFootprintCatalog.Models;

        // Assert
        Assert.Equal(Enum.GetValues<PropModel>().Order(), models.Order());
    }

    [Fact]
    public void Get_Throws_WhenTheModelIsNotCataloged()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => PropFootprintCatalog.Get((PropModel)(-1)));
    }
}
