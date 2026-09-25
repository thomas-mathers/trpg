using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class DistrictGeneratorTests
{
    [Fact]
    public void Generate_AddsPublicSeatsToDistrictLocation()
    {
        var worldId = Guid.NewGuid();

        var result = DistrictGenerator.Generate(
            DistrictType.CityCenter,
            Guid.NewGuid(),
            Guid.NewGuid(),
            worldId
        );

        Assert.Equal(3, result.Seats.Count);
        string[] expectedNames = ["Bench", "Stone Bench", "Low Wall"];
        Assert.Equal(expectedNames, result.Seats.Select(seat => seat.Name));
        Assert.All(
            result.Seats,
            seat =>
            {
                Assert.Equal(worldId, seat.WorldId);
                Assert.Equal(result.Location.Id, seat.LocationId);
                Assert.Null(seat.OccupantId);
            }
        );
    }
}
