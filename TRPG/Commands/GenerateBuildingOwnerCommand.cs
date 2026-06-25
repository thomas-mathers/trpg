using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Commands.Bootstrap;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateBuildingOwnerCommand {
    public required IReadOnlyList<Building> Buildings { get; init; }
    public required City City { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<Profession> Professions { get; init; }
    public required IReadOnlyList<Race> Races { get; init; }
    public required Guid WorldId { get; init; }
}

internal class GenerateBuildingOwnerCommandResult {
    public required BuildingOwner BuildingOwner { get; init; }
    public required Person Owner { get; init; }
}

internal class GenerateBuildingOwnerCommandHandler(
    AiClient client,
    ILogger<GenerateBuildingOwnerCommandHandler> logger) {
    public async Task<IReadOnlyList<GenerateBuildingOwnerCommandResult>> Handle(
        GenerateBuildingOwnerCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();
        
        var races = string.Join(", ", command.Races.Select(r => r.Name));
        var professions = string.Join(", ", command.Professions.Select(p => p.Name));
        var buildings = string.Join(", ", command.Buildings.Select(b => $"\"{b.Name}\""));

        var exampleRace = command.Races[0].Name;
        var exampleProfession = command.Professions[0].Name;
        var exampleBuilding = command.Buildings[0].Name;
        var example =
            $$"""{"Owners":[{"BuildingName":"{{exampleBuilding}}","Name":"Edric Hammerfall","Biography":"A seasoned tradesperson who has run this establishment for decades.","RaceName":"{{exampleRace}}","ProfessionName":"{{exampleProfession}}"}]}""";

        var chat = client.CreateChat<GenerateBuildingOwnerCommandHandler>(
            $"""
             You are a creative world-building assistant for a TRPG game generating content for: {command.Description}.
             Generate NPC owners for buildings in the city of {command.City.Name}.
             Respond with a JSON object with an "Owners" array. Each element has:
             - BuildingName: must exactly match one of the building names listed
             - Name: the NPC's full name
             - Biography: a short biography fitting their role as owner of this building
             - RaceName: must be one of: {races}
             - ProfessionName: must be one of: {professions}, pick one appropriate for the building
             Not every building needs an owner — only include ones that make sense. Do not use markdown.
             Example: {example}
             """);

        var schema = await chat.GetJson<BuildingOwnerListSchema>(
            $"Generate owners for these buildings in {command.City.Name}: {buildings}",
            cancellationToken
        );

        var buildingByName = command.Buildings.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
        var results = new List<GenerateBuildingOwnerCommandResult>();

        foreach (var ownerSchema in schema.Owners) {
            if (!buildingByName.TryGetValue(ownerSchema.BuildingName, out var building)) {
                continue;
            }

            var race = command.Races.FirstOrDefault(r =>
                           r.Name.Equals(ownerSchema.RaceName, StringComparison.OrdinalIgnoreCase)) ??
                       command.Races[Random.Shared.Next(command.Races.Count)];
            var profession =
                command.Professions.FirstOrDefault(p =>
                    p.Name.Equals(ownerSchema.ProfessionName, StringComparison.OrdinalIgnoreCase)) ??
                command.Professions[Random.Shared.Next(command.Professions.Count)];

            var owner = new Person {
                WorldId = command.WorldId,
                Name = ownerSchema.Name,
                Biography = ownerSchema.Biography,
                RaceId = race.Id,
                ProfessionId = profession.Id,
                BirthCityId = command.City.Id,
                BirthYear = Random.Shared.Next(900, 975),
                Gold = Random.Shared.Next(50, 500),
                Location = new Location {
                    CityId = command.City.Id,
                    BuildingId = building.Id,
                    Coordinates = new Point(0, 0)
                },
                Attributes = new Attributes {
                    Hp = new Meter(100, 100),
                    Ap = new Meter(10, 10),
                    Strength = 5,
                    Defense = 5,
                    Dexterity = 5,
                    Endurance = 5,
                    Intelligence = 5
                },
                Progression = new Progression {
                    Experience = new Meter(0, 100),
                    Level = 1
                }
            };

            results.Add(new GenerateBuildingOwnerCommandResult {
                Owner = owner,
                BuildingOwner = new BuildingOwner { BuildingId = building.Id, OwnerId = owner.Id }
            });
        }

        logger.LogDebug("GenerateBuildingOwners({City}) completed in {ElapsedSeconds:F1}s", command.City.Name,
            sw.Elapsed.TotalSeconds);
        
        return results;
    }
}