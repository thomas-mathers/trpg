using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

public class DistrictConnectorGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _cityId = Guid.NewGuid();

    [Fact]
    public void Generate_ReturnsTwoConnectors_PerOtherDistrict()
    {
        // Arrange
        var cityCenter = Builders.MakeDistrict(_cityId, DistrictType.CityCenter, worldId: _worldId);
        var residential = Builders.MakeDistrict(
            _cityId,
            DistrictType.Residential,
            worldId: _worldId
        );

        // Act
        var result = DistrictConnectorGenerator.Generate(cityCenter, [residential], _worldId);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Generate_ConnectsEachOtherDistrictToAndFromCityCenter()
    {
        // Arrange
        var cityCenter = Builders.MakeDistrict(_cityId, DistrictType.CityCenter, worldId: _worldId);
        var residential = Builders.MakeDistrict(
            _cityId,
            DistrictType.Residential,
            worldId: _worldId
        );

        // Act
        var result = DistrictConnectorGenerator.Generate(cityCenter, [residential], _worldId);

        // Assert
        Assert.Contains(
            result,
            c =>
                c.LocationId == residential.LocationId
                && c.DestinationLocationId == cityCenter.LocationId
        );
        Assert.Contains(
            result,
            c =>
                c.LocationId == cityCenter.LocationId
                && c.DestinationLocationId == residential.LocationId
        );
    }

    [Fact]
    public void Generate_ReturnsEmpty_WhenNoOtherDistricts()
    {
        // Arrange
        var cityCenter = Builders.MakeDistrict(_cityId, DistrictType.CityCenter, worldId: _worldId);

        // Act
        var result = DistrictConnectorGenerator.Generate(cityCenter, [], _worldId);

        // Assert
        Assert.Empty(result);
    }
}
