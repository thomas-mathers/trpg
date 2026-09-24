using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DistrictGeneratorTests
{
    [Fact]
    public void Generate_AddsPublicBenchToDistrictLocation()
    {
        var worldId = Guid.NewGuid();

        var result = DistrictGenerator.Generate(
            DistrictType.CityCenter,
            Guid.NewGuid(),
            Guid.NewGuid(),
            worldId
        );

        Assert.Equal(worldId, result.Bench.WorldId);
        Assert.Equal(result.Location.Id, result.Bench.LocationId);
        Assert.Equal("Bench", result.Bench.Name);
        Assert.Null(result.Bench.OccupantId);
    }
}
