using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class MapGeneratorTests
{
    [Fact]
    public void Generate_KeepsEveryCountrysStatesReachableWithoutCrossingAnotherCountry()
    {
        // Act
        var result = MapGenerator.Generate(
            worldWidth: 4000,
            worldHeight: 4000,
            numCityStates: 24,
            numNonCityStates: 12,
            numberOfCountries: 5
        );

        // Assert
        var countryIdByStateId = result.States.ToDictionary(s => s.Id, s => s.CountryId);
        var sameCountryNeighborsByStateId = result.States.ToDictionary(
            s => s.Id,
            _ => new List<Guid>()
        );

        foreach (var road in result.Roads)
        {
            if (
                countryIdByStateId[road.OriginStateId]
                != countryIdByStateId[road.DestinationStateId]
            )
            {
                continue;
            }

            sameCountryNeighborsByStateId[road.OriginStateId].Add(road.DestinationStateId);
            sameCountryNeighborsByStateId[road.DestinationStateId].Add(road.OriginStateId);
        }

        foreach (var country in result.States.GroupBy(s => s.CountryId))
        {
            var stateIds = country.Select(s => s.Id).ToHashSet();
            var reachable = ReachableStateIds(stateIds.First(), sameCountryNeighborsByStateId);

            Assert.True(
                stateIds.SetEquals(reachable),
                $"Country {country.Key} has a state only reachable by leaving the country."
            );
        }
    }

    private static HashSet<Guid> ReachableStateIds(
        Guid start,
        IReadOnlyDictionary<Guid, List<Guid>> neighborsByStateId
    )
    {
        var visited = new HashSet<Guid> { start };
        var queue = new Queue<Guid>();
        queue.Enqueue(start);

        while (queue.TryDequeue(out var current))
        {
            foreach (var neighbor in neighborsByStateId[current])
            {
                if (visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visited;
    }
}
