using Microsoft.EntityFrameworkCore;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Worlds.Generators;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Commands;

public class SyncRestockPolicyCommand
{
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
    public required TimeSpan CurrentPlaytime { get; init; }
}

internal class SyncRestockPolicyCommandHandler(
    TrpgDbContext context,
    ItemGenerator itemGenerator,
    IQueryHandler<
        GetWorkstationsByLocationIdQuery,
        IReadOnlyCollection<Workstation>
    > getWorkstationsByLocationId,
    IQueryHandler<GetBuildingTypeByLocationIdQuery, BuildingType?> getBuildingTypeByLocationId
) : ICommandHandler<SyncRestockPolicyCommand>
{
    public async Task Handle(
        SyncRestockPolicyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var workstations = await getWorkstationsByLocationId.Handle(
            new GetWorkstationsByLocationIdQuery { LocationId = command.LocationId },
            cancellationToken
        );
        if (workstations.Count == 0)
        {
            return;
        }

        var buildingType = await getBuildingTypeByLocationId.Handle(
            new GetBuildingTypeByLocationIdQuery { LocationId = command.LocationId },
            cancellationToken
        );
        if (buildingType == null)
        {
            return;
        }

        foreach (var workstation in workstations)
        {
            await SyncWorkstation(workstation.Id, buildingType.Value, command, cancellationToken);
        }
    }

    private async Task SyncWorkstation(
        Guid workstationId,
        BuildingType buildingType,
        SyncRestockPolicyCommand command,
        CancellationToken cancellationToken
    )
    {
        var policy = await context.RestockPolicies.FirstOrDefaultAsync(
            p => p.WorkstationId == workstationId,
            cancellationToken
        );
        if (policy == null)
        {
            return;
        }

        var hasTriggered = RecurringScheduling.HasTriggered(
            policy.TriggerHour,
            policy.SpecificDay,
            policy.LastSyncPlaytime,
            command.CurrentPlaytime
        );
        if (!hasTriggered)
        {
            return;
        }

        var currentItems = await context
            .Items.Where(i =>
                i.Ownership.OwnerId == workstationId
                && i.Ownership.OwnerType == OwnerType.Workstation
            )
            .ToListAsync(cancellationToken);

        var fillResult = TradeStockFiller.Fill(
            itemGenerator,
            buildingType,
            currentItems,
            policy.WorldId,
            command.PlayerLevel
        );

        foreach (var item in fillResult.ItemsToAdd)
        {
            item.Ownership.OwnerId = workstationId;
            item.Ownership.OwnerType = OwnerType.Workstation;
        }
        context.Items.AddRange(fillResult.ItemsToAdd);

        foreach (var (itemId, quantity) in fillResult.QuantityIncreasesByItemId)
        {
            var existing = currentItems.First(i => i.Id == itemId);
            existing.Quantity = quantity;
        }

        if (buildingType == BuildingType.Inn)
        {
            await RegenerateMissingRoomKeys(workstationId, policy.WorldId, cancellationToken);
        }

        policy.LastSyncPlaytime = command.CurrentPlaytime;

        await context.SaveChangesAsync(cancellationToken);
    }

    // A guest room whose only key was stolen or never returned would otherwise stay
    // unbookable forever; the innkeeper has a replacement made on the same cadence as
    // restocking. Never revokes an already-issued key — a door can have more than one.
    private async Task RegenerateMissingRoomKeys(
        Guid workstationId,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var buildingId = await (
            from workstation in context.Props.OfType<Workstation>().AsNoTracking()
            where workstation.Id == workstationId
            join room in context.Rooms.AsNoTracking()
                on workstation.LocationId equals room.LocationId
            select (Guid?)room.BuildingId
        ).FirstOrDefaultAsync(cancellationToken);
        if (buildingId == null)
        {
            return;
        }

        var doorsByRoom = await (
            from door in context.DoorConnectors.AsNoTracking()
            join connector in context.LocationConnectors.AsNoTracking()
                on door.ConnectorId equals connector.Id
            join room in context.Rooms.AsNoTracking()
                on connector.DestinationLocationId equals room.LocationId
            where room.BuildingId == buildingId.Value
            select new { room.Name, DoorConnectorId = door.Id }
        ).ToListAsync(cancellationToken);
        if (doorsByRoom.Count == 0)
        {
            return;
        }

        var doorConnectorIds = doorsByRoom.Select(d => d.DoorConnectorId).ToArray();
        var doorsWithSpareKey = await (
            from doorConnectorKey in context.DoorConnectorKeys.AsNoTracking()
            where doorConnectorIds.AsEnumerable().Contains(doorConnectorKey.DoorConnectorId)
            join item in context.Items.AsNoTracking() on doorConnectorKey.ItemId equals item.Id
            where item.Ownership.OwnerType == OwnerType.Workstation
            select doorConnectorKey.DoorConnectorId
        ).ToListAsync(cancellationToken);
        var doorsWithSpareKeySet = doorsWithSpareKey.ToHashSet();

        foreach (
            var door in doorsByRoom.Where(d => !doorsWithSpareKeySet.Contains(d.DoorConnectorId))
        )
        {
            var replacementKey = new Key
            {
                WorldId = worldId,
                Name = $"Key to {door.Name}",
                Description = $"A replacement key to the {door.Name}.",
                Quantity = 1,
                Ownership = new ItemOwnership
                {
                    OwnerId = workstationId,
                    OwnerType = OwnerType.Workstation,
                },
            };
            context.Items.Add(replacementKey);
            context.DoorConnectorKeys.Add(
                new DoorConnectorKey
                {
                    ItemId = replacementKey.Id,
                    DoorConnectorId = door.DoorConnectorId,
                    WorldId = worldId,
                }
            );
        }
    }
}
