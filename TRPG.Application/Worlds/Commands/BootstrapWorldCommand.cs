using Microsoft.Extensions.Logging;
using TRPG.Application.Worlds.Generators;
using TRPG.Data;

namespace TRPG.Application.Worlds.Commands;

public record BootstrapWorldResult(Guid WorldId, Guid PlayerId);

public class BootstrapWorldCommandHandler(
    TrpgDbContext context,
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
        context.InventoryItems.AddRange(world.InventoryItems);
        context.Rooms.AddRange(world.Rooms);
        context.Props.AddRange(world.Props);
        context.CreatureSkills.AddRange(world.Skills);
        context.CreatureAbilities.AddRange(world.Abilities);
        context.Jobs.AddRange(world.Jobs);
        context.CreatureKnowledge.AddRange(world.Knowledge);
        context.RoomConnectorKeys.AddRange(world.RoomConnectorKeys);
        context.Relationships.AddRange(world.Relationships);

        if (player != null)
        {
            context.Creatures.Add(player.Creature);
            context.Items.AddRange(player.Items);
            context.InventoryItems.AddRange(player.InventoryItems);
            context.CreatureSkills.AddRange(player.Skills);
            context.CreatureAbilities.AddRange(player.Abilities);
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Bootstrap saved {WorldId}", world.World.Id);

        return new BootstrapWorldResult(world.World.Id, world.World.PlayerId ?? Guid.Empty);
    }
}
