using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncRestockPolicyCommand
{
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
    public required TimeSpan CurrentPlaytime { get; init; }
}

internal class SyncRestockPolicyCommandHandler(
    ILocationSimulationDbContext context,
    ItemGenerator itemGenerator,
    IQueryHandler<
        GetWorkstationsByLocationIdQuery,
        IReadOnlyCollection<Workstation>
    > getWorkstationsByLocationId,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<
        GetGuestRoomDoorsByBuildingIdQuery,
        IReadOnlyList<GuestRoomDoor>
    > getGuestRoomDoors,
    IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>> getInventoryItemsByOwner,
    IQueryHandler<
        GetWorkstationOwnedItemIdsQuery,
        IReadOnlyDictionary<Guid, Guid>
    > getWorkstationOwnedItemIds,
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<UpdateItemQuantitiesCommand> updateItemQuantities,
    ICommandHandler<IssueReplacementRoomKeyCommand> issueReplacementRoomKey
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

        var currentItems = await getInventoryItemsByOwner.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(workstationId, OwnerType.Workstation),
            },
            cancellationToken
        );

        var fillResult = TradeStockFiller.Fill(
            itemGenerator,
            buildingType,
            currentItems,
            policy.WorldId,
            command.PlayerLevel
        );

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        if (fillResult.ItemsToAdd.Count > 0)
        {
            foreach (var item in fillResult.ItemsToAdd)
            {
                item.Ownership.OwnerId = workstationId;
                item.Ownership.OwnerType = OwnerType.Workstation;
            }
            await addItems.Handle(
                new AddItemsCommand { Items = fillResult.ItemsToAdd },
                cancellationToken
            );
        }

        if (fillResult.QuantityIncreasesByItemId.Count > 0)
        {
            await updateItemQuantities.Handle(
                new UpdateItemQuantitiesCommand
                {
                    Updates = fillResult
                        .QuantityIncreasesByItemId.Select(kv => new ItemQuantityUpdate(
                            kv.Key,
                            kv.Value
                        ))
                        .ToArray(),
                },
                cancellationToken
            );
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

        transaction.Complete();
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
        var candidateKeyItemIds = guestRoomDoors
            .SelectMany(door => door.CandidateKeyItemIds)
            .ToArray();
        var workstationIdsByItemId = await getWorkstationOwnedItemIds.Handle(
            new GetWorkstationOwnedItemIdsQuery { ItemIds = candidateKeyItemIds },
            cancellationToken
        );

        foreach (
            var door in guestRoomDoors.Where(d =>
                !d.CandidateKeyItemIds.Any(id => workstationIdsByItemId.ContainsKey(id))
            )
        )
        {
            await issueReplacementRoomKey.Handle(
                new IssueReplacementRoomKeyCommand
                {
                    WorkstationId = workstationId,
                    DoorConnectorId = door.DoorConnectorId,
                    WorldId = worldId,
                    RoomName = door.RoomName,
                },
                cancellationToken
            );
        }
    }
}
