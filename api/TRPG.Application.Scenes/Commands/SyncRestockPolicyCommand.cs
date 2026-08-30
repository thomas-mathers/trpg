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
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<
        GetGuestRoomDoorsByBuildingIdQuery,
        IReadOnlyList<GuestRoomDoor>
    > getGuestRoomDoors
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

        var building = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = command.LocationId },
            cancellationToken
        );
        if (building == null)
        {
            return;
        }

        foreach (var workstation in workstations)
        {
            await SyncWorkstation(workstation.Id, building, command, cancellationToken);
        }
    }

    private async Task SyncWorkstation(
        Guid workstationId,
        BuildingIdentity building,
        SyncRestockPolicyCommand command,
        CancellationToken cancellationToken
    )
    {
        var buildingType = building.BuildingType;
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
            await RegenerateMissingRoomKeys(
                workstationId,
                building.Id,
                policy.WorldId,
                cancellationToken
            );
        }

        policy.LastSyncPlaytime = command.CurrentPlaytime;

        await context.SaveChangesAsync(cancellationToken);
    }

    // Mints a replacement on the same cadence as restocking; never revokes an already-issued key.
    private async Task RegenerateMissingRoomKeys(
        Guid workstationId,
        Guid buildingId,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var guestRoomDoors = await getGuestRoomDoors.Handle(
            new GetGuestRoomDoorsByBuildingIdQuery { BuildingId = buildingId },
            cancellationToken
        );

        foreach (var door in guestRoomDoors.Where(d => d.SpareKeyItemId == null))
        {
            var replacementKey = new Key
            {
                WorldId = worldId,
                Name = $"Key to {door.RoomName}",
                Description = $"A replacement key to the {door.RoomName}.",
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
