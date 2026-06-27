using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateWorldCommand {
    public int AttackCount { get; init; }
    public int BuildingsPerCity { get; init; }
    public int MaxCountries { get; init; }
    public int MinCountries { get; init; }
    public required string Description { get; init; }
    public int FactionCount { get; init; }
    public int MaxCities { get; init; }
    public int MinCities { get; init; }
    public int ProfessionCount { get; init; }
    public int RaceCount { get; init; }
    public int SupportCount { get; init; }
}

internal class GenerateWorldCommandResult {
    public required IReadOnlyList<Attack> Attacks { get; init; }
    public required IReadOnlyList<BuildingOwner> BuildingOwners { get; init; }
    public required IReadOnlyList<Building> Buildings { get; init; }
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<Faction> Factions { get; init; }
    public required IReadOnlyList<Person> Persons { get; init; }
    public required IReadOnlyList<Profession> Professions { get; init; }
    public required IReadOnlyList<Race> Races { get; init; }
    public required IReadOnlyList<Road> Roads { get; init; }
    public required IReadOnlyList<SkillPrerequisite> SkillPrerequisites { get; init; }
    public required IReadOnlyList<Support> Supports { get; init; }
    public required World World { get; init; }
}

internal class GenerateWorldCommandHandler(
    GenerateGeographyCommandHandler geographyHandler,
    GenerateRacesCommandHandler racesHandler,
    GenerateProfessionsCommandHandler professionsHandler,
    GenerateFactionsCommandHandler factionsHandler,
    GenerateBuildingsCommandHandler buildingsHandler,
    GenerateSkillsCommandHandler skillsHandler,
    GenerateBuildingOwnerCommandHandler buildingOwnerHandler,
    ILogger<GenerateWorldCommandHandler> logger
) {
    public async Task<GenerateWorldCommandResult> Handle(
        GenerateWorldCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();

        var geography = await geographyHandler.Handle(
            new GenerateGeographyCommand {
                Description = command.Description,
                MaxCities = command.MaxCities,
                MaxCountries = command.MaxCountries,
                MinCities = command.MinCities,
                MinCountries = command.MinCountries
            },
            cancellationToken
        );

        var worldId = geography.World.Id;

        var races = await racesHandler.Handle(
            new GenerateRacesCommand { Count = command.RaceCount, Description = command.Description, WorldId = worldId },
            cancellationToken
        );
        var professions = await professionsHandler.Handle(
            new GenerateProfessionsCommand { Count = command.ProfessionCount, Description = command.Description, WorldId = worldId },
            cancellationToken
        );
        var factions = await factionsHandler.Handle(
            new GenerateFactionsCommand { Count = command.FactionCount, Description = command.Description, WorldId = worldId },
            cancellationToken
        );

        var existingBuildingNames = new List<string>();
        var cityBuildingsList = new List<IReadOnlyList<Building>>();
        foreach (var city in geography.Cities) {
            var cityBuildings = await buildingsHandler.Handle(
                new GenerateBuildingsCommand {
                    City = city,
                    Count = command.BuildingsPerCity,
                    Description = command.Description,
                    ExistingBuildingNames = existingBuildingNames.AsReadOnly()
                },
                cancellationToken
            );
            cityBuildingsList.Add(cityBuildings);
            existingBuildingNames.AddRange(cityBuildings.Select(b => b.Name));
        }

        var buildings = cityBuildingsList.SelectMany(b => b).ToArray();

        var skillsResult = await skillsHandler.Handle(
            new GenerateSkillsCommand {
                AttackCount = command.AttackCount,
                Description = command.Description,
                SupportCount = command.SupportCount,
                WorldId = worldId
            },
            cancellationToken
        );

        var ownerResults = new List<GenerateBuildingOwnerCommandResult>();
        foreach (var (city, cityBuildings) in geography.Cities.Zip(cityBuildingsList)) {
            var results = await buildingOwnerHandler.Handle(
                new GenerateBuildingOwnerCommand {
                    Buildings = cityBuildings,
                    City = city,
                    Description = command.Description,
                    Professions = professions,
                    Races = races,
                    WorldId = worldId
                },
                cancellationToken
            );
            ownerResults.AddRange(results);
        }

        logger.LogDebug("GenerateWorld completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new GenerateWorldCommandResult {
            World = geography.World,
            Countries = geography.Countries,
            Cities = geography.Cities,
            Roads = geography.Roads,
            Races = races,
            Professions = professions,
            Factions = factions,
            Attacks = skillsResult.Attacks,
            Supports = skillsResult.Supports,
            SkillPrerequisites = skillsResult.Prerequisites,
            Buildings = buildings,
            Persons = ownerResults.Select(r => r.Owner).ToList(),
            BuildingOwners = ownerResults.Select(r => r.BuildingOwner).ToList()
        };
    }
}
