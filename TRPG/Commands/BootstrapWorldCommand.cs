using Microsoft.Extensions.Logging;
using TRPG.Data;

namespace TRPG.Commands;

internal class BootstrapWorldCommand {
    public required string Description { get; init; }
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
            new GenerateWorldCommand { Description = command.Description },
            cancellationToken
        );

        context.Worlds.Add(result.World);
        context.Countries.AddRange(result.Countries);
        context.Provinces.AddRange(result.Provinces);
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
