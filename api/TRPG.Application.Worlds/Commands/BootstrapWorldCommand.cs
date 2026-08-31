using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data;

namespace TRPG.Application.Worlds.Commands;

public record BootstrapWorldResult(Guid WorldId, Guid PlayerId);

public class BootstrapWorldCommand
{
    public required WorldGeneratorResult World { get; init; }
    public CreatureGeneratorResult? Player { get; init; }
    public required QuestGeneratorResult Quests { get; init; }
}

internal class BootstrapWorldCommandHandler(
    TrpgDbContext context,
    TradeStockGenerator tradeStockGenerator,
    ILogger<BootstrapWorldCommandHandler> logger
) : ICommandHandler<BootstrapWorldCommand, BootstrapWorldResult>
{
    public async Task<BootstrapWorldResult> Handle(
        BootstrapWorldCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var world = command.World;
        var player = command.Player;
        var quests = command.Quests;

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
        context.EncounterGroups.AddRange(world.EncounterGroups);
        context.EncounterGroupMembers.AddRange(world.EncounterGroupMembers);
        context.Buildings.AddRange(world.Buildings);
        context.Creatures.AddRange(world.Creatures);
        context.NpcProfiles.AddRange(world.NpcProfiles);
        context.BuildingOwners.AddRange(world.BuildingOwners);
        context.Items.AddRange(world.Items);
        context.Rooms.AddRange(world.Rooms);
        context.Locations.AddRange(world.Locations);
        context.Props.AddRange(world.Props);
        context.LocationConnectors.AddRange(world.LocationConnectors);
        context.DoorConnectors.AddRange(world.DoorConnectors);
        context.TravelConnectors.AddRange(world.TravelConnectors);
        var tradeStock = tradeStockGenerator.Generate(
            world.Props,
            world.Rooms,
            world.Buildings,
            world.World.Id,
            player?.Creature.Level ?? 1
        );
        context.Items.AddRange(tradeStock.Items);
        context.RestockPolicies.AddRange(tradeStock.RestockPolicies);
        context.CreatureSpawners.AddRange(world.CreatureSpawners);
        context.CreatureSkills.AddRange(world.Skills);
        context.CreatureJobs.AddRange(world.Jobs);
        context.CreatureKnowledge.AddRange(world.Knowledge);
        context.DoorConnectorKeys.AddRange(world.DoorConnectorKeys);
        context.Relationships.AddRange(world.Relationships);
        context.Quests.AddRange(quests.Quests);
        context.QuestObjectives.AddRange(quests.Objectives);

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
