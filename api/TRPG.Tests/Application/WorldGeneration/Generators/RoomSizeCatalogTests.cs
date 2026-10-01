using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class RoomSizeCatalogTests
{
    public static TheoryData<BuildingType> BuildingTypes => new(Enum.GetValues<BuildingType>());

    public static TheoryData<RoomRole> RoomRoles => new(Enum.GetValues<RoomRole>());

    [Theory]
    [MemberData(nameof(BuildingTypes))]
    public void Get_ReturnsAPositiveRange_ForEveryBuildingType(BuildingType buildingType)
    {
        // Act
        var limits = RoomSizeCatalog.Get(buildingType, null);

        // Assert
        Assert.True(limits.MinimumArea > 0);
        Assert.True(limits.MaximumArea > limits.MinimumArea);
    }

    [Theory]
    [MemberData(nameof(RoomRoles))]
    public void Get_ReturnsAPositiveRange_ForEveryRoomRole(RoomRole role)
    {
        // Act
        var limits = RoomSizeCatalog.Get(BuildingType.Crypt, role);

        // Assert
        Assert.True(limits.MinimumArea > 0);
        Assert.True(limits.MaximumArea > limits.MinimumArea);
    }

    [Fact]
    public void Get_PrefersTheRoomRole_WhenOneIsGiven()
    {
        // Act
        var limits = RoomSizeCatalog.Get(BuildingType.House, RoomRole.BossChamber);

        // Assert
        Assert.Equal(80, limits.MinimumArea);
    }
}
