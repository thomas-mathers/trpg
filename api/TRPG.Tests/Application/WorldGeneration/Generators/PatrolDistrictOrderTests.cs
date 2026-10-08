using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class PatrolDistrictOrderTests
{
    private readonly Guid _hub = Guid.NewGuid();
    private readonly Guid _north = Guid.NewGuid();
    private readonly Guid _east = Guid.NewGuid();
    private readonly Guid _island = Guid.NewGuid();

    [Fact]
    public void Build_StartsAtTheMostConnectedDistrict()
    {
        // Arrange
        var connectors = Both(_hub, _north).Concat(Both(_hub, _east)).ToArray();

        // Act
        var order = PatrolDistrictOrder.Build([_north, _hub, _east], connectors);

        // Assert
        Assert.Equal(_hub, order[0]);
    }

    [Fact]
    public void Build_VisitsEveryDistrictOnce_WhenSomeAreUnreachable()
    {
        // Arrange
        var connectors = Both(_hub, _north);

        // Act
        var order = PatrolDistrictOrder.Build([_hub, _north, _east, _island], connectors);

        // Assert
        Assert.Equivalent(new[] { _hub, _north, _east, _island }, order);
    }

    [Fact]
    public void Build_FollowsConnectorsDepthFirst()
    {
        // Arrange
        var connectors = Both(_hub, _north).Concat(Both(_north, _east)).ToArray();

        // Act
        var order = PatrolDistrictOrder.Build([_hub, _north, _east], connectors);

        // Assert
        Assert.Equal([_north, _hub, _east], order);
    }

    private static LocationConnector[] Both(Guid first, Guid second) =>
        [
            Builders.MakeLocationConnector(first, second),
            Builders.MakeLocationConnector(second, first),
        ];
}
