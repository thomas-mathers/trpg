using Microsoft.Extensions.Logging;
using TRPG.Data;
using TRPG.Generators;

namespace TRPG.Commands;

internal class BootstrapWorldCommand {
    public required string Description { get; init; }
    public int FactionCount { get; init; } = WorldGenerationDefaults.FactionCount;
    public int HousesPerCity { get; init; } = WorldGenerationDefaults.HousesPerCity;
    public int MaxCities { get; init; } = WorldGenerationDefaults.MaxCities;
    public int MaxCountries { get; init; } = WorldGenerationDefaults.MaxCountries;
    public int MinCities { get; init; } = WorldGenerationDefaults.MinCities;
    public int MinCountries { get; init; } = WorldGenerationDefaults.MinCountries;
    public int RaceCount { get; init; } = WorldGenerationDefaults.RaceCount;
}

internal class BootstrapWorldCommandHandler(
    TrpgDbContext context,
    WorldGenerator worldHandler,
    ILogger<BootstrapWorldCommandHandler> logger
) {
    public async Task<WorldGeneratorResult> Handle(
        BootstrapWorldCommand command,
        CancellationToken cancellationToken
    ) {
        var result = await worldHandler.Generate(
            new WorldGeneratorInput {
                Description = command.Description,
                FactionCount = command.FactionCount,
                HousesPerCity = command.HousesPerCity,
                MaxCities = command.MaxCities,
                MaxCountries = command.MaxCountries,
                MinCities = command.MinCities,
                MinCountries = command.MinCountries,
                RaceCount = command.RaceCount
            },
            cancellationToken
        );

        context.Worlds.Add(result.World);
        context.Countries.AddRange(result.Countries);
        context.Cities.AddRange(result.Cities);
        context.Races.AddRange(result.Races);
        context.Factions.AddRange(result.Factions);
        context.FactionMembers.AddRange(result.FactionMembers);
        context.Roads.AddRange(result.Roads);
        context.Buildings.AddRange(result.Buildings);
        context.Persons.AddRange(result.Persons);
        context.BuildingOwners.AddRange(result.BuildingOwners);
        context.Items.AddRange(result.Items);
        context.InventoryItems.AddRange(result.InventoryItems);
        context.Rooms.AddRange(result.Rooms);
        context.Props.AddRange(result.Props);
        context.PersonSkills.AddRange(result.Skills);
        context.PersonAbilities.AddRange(result.Abilities);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Bootstrap saved {WorldId}", result.World.Id);

        return result;
    }
}