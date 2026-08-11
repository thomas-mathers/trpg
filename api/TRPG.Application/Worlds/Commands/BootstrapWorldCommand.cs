using Microsoft.Extensions.Logging;
using TRPG.Application.Worlds.Generators;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Commands;

public record BootstrapWorldResult(Guid WorldId, Guid PlayerId);

public class BootstrapWorldCommandHandler(
    TrpgDbContext context,
    TradeStockGenerator tradeStockGenerator,
    ILogger<BootstrapWorldCommandHandler> logger
)
{
    public async Task<BootstrapWorldResult> Handle(
        WorldGeneratorResult world,
        CreatureGeneratorResult? player,
        CancellationToken cancellationToken
    )
    {
        if (player != null)
        {
            world.World.PlayerId = player.Creature.Id;
        }

        context.Worlds.Add(world.World);
        context.Countries.AddRange(world.Countries);
        context.States.AddRange(world.States);
        context.Cities.AddRange(world.Cities);
        context.Districts.AddRange(world.Districts);
        context.Factions.AddRange(world.Factions);
        context.FactionMembers.AddRange(world.FactionMembers);
        context.Roads.AddRange(world.Roads);
        context.Buildings.AddRange(world.Buildings);
        context.Creatures.AddRange(world.Creatures);
        context.BuildingOwners.AddRange(world.BuildingOwners);
        context.Items.AddRange(world.Items);
        context.Rooms.AddRange(world.Rooms);
        context.Locations.AddRange(world.Locations);
        context.Props.AddRange(world.Props);
        context.Items.AddRange(
            tradeStockGenerator.Generate(world.Props, world.Rooms, world.Buildings, world.World.Id)
        );
        context.CreatureSkills.AddRange(world.Skills);
        context.CreatureJobs.AddRange(world.Jobs);
        context.CreatureKnowledge.AddRange(world.Knowledge);
        context.LocationConnectorKeys.AddRange(world.LocationConnectorKeys);
        context.Relationships.AddRange(world.Relationships);

        if (player != null)
        {
            context.Creatures.Add(player.Creature);
            context.Items.AddRange(player.Items);
            context.CreatureSkills.AddRange(player.Skills);
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Bootstrap saved {WorldId}", world.World.Id);

        return new BootstrapWorldResult(world.World.Id, world.World.PlayerId ?? Guid.Empty);
    }
}
