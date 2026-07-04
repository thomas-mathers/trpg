using Microsoft.Extensions.Logging;
using TRPG.Data;
using TRPG.Generators;

namespace TRPG.Commands;

internal record BootstrapWorldResult(Guid WorldId, Guid PlayerId);

internal class BootstrapWorldCommandHandler(
    TrpgDbContext context,
    ILogger<BootstrapWorldCommandHandler> logger
) {
    public async Task<BootstrapWorldResult> Handle(
        WorldGeneratorResult world,
        PersonGeneratorResult? player,
        CancellationToken cancellationToken
    ) {
        if (player != null) {
            world.World.PlayerId = player.Person.Id;
        }

        context.Worlds.Add(world.World);
        context.Countries.AddRange(world.Countries);
        context.States.AddRange(world.States);
        context.Cities.AddRange(world.Cities);
        context.Districts.AddRange(world.Districts);
        context.Races.AddRange(world.Races);
        context.Factions.AddRange(world.Factions);
        context.FactionMembers.AddRange(world.FactionMembers);
        context.Roads.AddRange(world.Roads);
        context.Buildings.AddRange(world.Buildings);
        context.Persons.AddRange(world.Persons);
        context.BuildingOwners.AddRange(world.BuildingOwners);
        context.Items.AddRange(world.Items);
        context.InventoryItems.AddRange(world.InventoryItems);
        context.Rooms.AddRange(world.Rooms);
        context.Props.AddRange(world.Props);
        context.PersonSkills.AddRange(world.Skills);
        context.PersonAbilities.AddRange(world.Abilities);
        context.Jobs.AddRange(world.Jobs);
        context.RoomConnectorKeys.AddRange(world.RoomConnectorKeys);

        if (player != null) {
            context.Persons.Add(player.Person);
            context.Items.AddRange(player.Items);
            context.InventoryItems.AddRange(player.InventoryItems);
            context.PersonSkills.AddRange(player.Skills);
            context.PersonAbilities.AddRange(player.Abilities);
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Bootstrap saved {WorldId}", world.World.Id);

        return new BootstrapWorldResult(world.World.Id, world.World.PlayerId ?? Guid.Empty);
    }
}
