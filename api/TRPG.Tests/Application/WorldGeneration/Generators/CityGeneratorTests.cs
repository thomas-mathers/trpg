using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class CityGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly City _city;
    private readonly State _state;
    private readonly IReadOnlyList<District> _districts;
    private readonly IReadOnlyDictionary<Guid, Location> _locationsById;
    private readonly WorldGeneratorInput _generatorInput = new()
    {
        Description = "test",
        HousesPerCity = 0,
        MinHouseholdSize = 1,
        MaxHouseholdSize = 4,
        MinFactionMembers = 1,
        MaxFactionMembers = 1,
    };
    private readonly CityGenerator _cityGenerator = MakeCityGenerator();

    public CityGeneratorTests()
    {
        var country = Builders.MakeCountry(_worldId);
        _state = Builders.MakeState(country.Id, _worldId);
        _city = Builders.MakeCity(_state.Id, country.Id, worldId: _worldId);
        _districts =
        [
            Builders.MakeDistrict(_city.Id, DistrictType.Residential, worldId: _worldId),
            Builders.MakeDistrict(_city.Id, worldId: _worldId),
        ];
        _locationsById = _districts.ToDictionary(
            district => district.LocationId,
            district =>
                Builders.MakeLocation(
                    _worldId,
                    _state.Id,
                    cityId: _city.Id,
                    districtId: district.Id,
                    id: district.LocationId
                )
        );
    }

    private static CityGenerator MakeCityGenerator()
    {
        var itemGenerator = new ItemGenerator(
            new WeaponGenerator(),
            new ArmorGenerator(),
            new AccessoryGenerator(),
            new ConsumableGenerator(),
            new AmmoGenerator()
        );
        var creatureGenerator = new CreatureGenerator(
            itemGenerator,
            new TestOptionsSnapshot<CreatureGeneratorOptions>(new CreatureGeneratorOptions())
        );
        var householdGenerator = new HouseholdGenerator(new BuildingGenerator(), creatureGenerator);
        var creatureGroupGenerator = new CreatureGroupGenerator(creatureGenerator);
        return new CityGenerator(
            new BuildingGenerator(),
            householdGenerator,
            creatureGroupGenerator
        );
    }

    private CityGeneratorInput MakeInput(IReadOnlyList<Faction> namedFactions)
    {
        return new CityGeneratorInput
        {
            WorldId = _worldId,
            City = _city,
            State = _state,
            DominantRace = CreatureType.Human,
            Districts = _districts,
            LocationsById = _locationsById,
            NamedFactions = namedFactions,
            GeneratorInput = _generatorInput,
        };
    }

    [Fact]
    public void Generate_NeverProducesMoreGuildHallsThanNamedFactions()
    {
        // Arrange — 2 factions, but 5 simulated cities all competing for a guild hall
        var namedFactions = Enumerable
            .Range(0, 2)
            .Select(_ => Builders.MakeFaction(_worldId))
            .ToArray();

        // Act
        var guildHallCounts = Enumerable
            .Range(0, 5)
            .Select(_ =>
                _cityGenerator
                    .Generate(MakeInput(namedFactions))
                    .Buildings.Count(b => b.BuildingType == BuildingType.GuildHall)
            )
            .ToArray();

        // Assert — only the first 2 cities get a guild hall, the rest get none
        Assert.Equal([1, 1, 0, 0, 0], guildHallCounts);
    }

    [Fact]
    public void Generate_NeverAssignsTheSameFactionAsLeaderTwice()
    {
        // Arrange
        var namedFactions = Enumerable
            .Range(0, 3)
            .Select(_ => Builders.MakeFaction(_worldId))
            .ToArray();

        // Act
        var leaderFactionIds = Enumerable
            .Range(0, 3)
            .SelectMany(_ =>
                _cityGenerator
                    .Generate(MakeInput(namedFactions))
                    .FactionMembers.Where(m => m.Role == FactionRole.Leader)
            )
            .Select(m => m.FactionId)
            .ToArray();

        // Assert
        Assert.Equal(leaderFactionIds.Length, leaderFactionIds.Distinct().Count());
    }
}
