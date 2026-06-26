using Microsoft.Extensions.Logging;
using TRPG.Data;

namespace TRPG.Commands;

internal class BootstrapWorldCommand {
    public int AttackCount { get; init; } = WorldGenerationDefaults.AttackCount;
    public int BuildingsPerCity { get; init; } = WorldGenerationDefaults.BuildingsPerCity;
    public int CountryCount { get; init; } = WorldGenerationDefaults.CountryCount;
    public required string Description { get; init; }
    public int FactionCount { get; init; } = WorldGenerationDefaults.FactionCount;
    public int MaxCities { get; init; } = WorldGenerationDefaults.MaxCities;
    public int MinCities { get; init; } = WorldGenerationDefaults.MinCities;
    public int ProfessionCount { get; init; } = WorldGenerationDefaults.ProfessionCount;
    public int RaceCount { get; init; } = WorldGenerationDefaults.RaceCount;
    public int SupportCount { get; init; } = WorldGenerationDefaults.SupportCount;
}

internal class BootstrapWorldCommandHandler(
    TrpgDbContext context,
    GenerateWorldCommandHandler generateWorldHandler,
    ILogger<BootstrapWorldCommandHandler> logger
) {
    public async Task<GenerateWorldCommandResult> Handle(
        BootstrapWorldCommand command,
        CancellationToken cancellationToken
    ) {
        var result = await generateWorldHandler.Handle(
            new GenerateWorldCommand {
                AttackCount = command.AttackCount,
                BuildingsPerCity = command.BuildingsPerCity,
                CountryCount = command.CountryCount,
                Description = command.Description,
                FactionCount = command.FactionCount,
                MaxCities = command.MaxCities,
                MinCities = command.MinCities,
                ProfessionCount = command.ProfessionCount,
                RaceCount = command.RaceCount,
                SupportCount = command.SupportCount
            },
            cancellationToken
        );

        context.Worlds.Add(result.World);
        context.Countries.AddRange(result.Countries);
        context.Cities.AddRange(result.Cities);
        context.Races.AddRange(result.Races);
        context.Professions.AddRange(result.Professions);
        context.Factions.AddRange(result.Factions);
        context.Skills.AddRange([..result.Attacks, ..result.Supports]);
        context.SkillPrerequisites.AddRange(result.SkillPrerequisites);
        context.TravelRoutes.AddRange(result.TravelRoutes);
        context.Buildings.AddRange(result.Buildings);
        context.Persons.AddRange(result.Persons);
        context.BuildingOwners.AddRange(result.BuildingOwners);
        
        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Bootstrap saved {WorldId}", result.World.Id);

        return result;
    }
}
