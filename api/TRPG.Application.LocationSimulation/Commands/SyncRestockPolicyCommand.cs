using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Scheduling;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncRestockPolicyCommand
{
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
    public required GameInstant CurrentGameTime { get; init; }
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
    ICommandHandler<RestockGoldCommand> restockGold,
    ICommandHandler<UpdateItemQuantitiesCommand> updateItemQuantities,
    ICommandHandler<IssueReplacementRoomKeysCommand> issueReplacementRoomKeys
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

        var workstationIds = workstations.Select(workstation => workstation.Id).ToArray();
        var policies = await context
            .RestockPolicies.Where(policy =>
                workstationIds.AsEnumerable().Contains(policy.WorkstationId)
            )
            .ToArrayAsync(cancellationToken);

        var duePolicies = policies.Where(policy =>
            RecurringScheduling.HasTriggered(
                policy.Schedule,
                policy.LastSyncGameTime,
                command.CurrentGameTime
            )
        );

        foreach (var policy in duePolicies)
        {
            await SyncWorkstation(policy, building, command, cancellationToken);
        }
    }

    private async Task SyncWorkstation(
        RestockPolicy policy,
        BuildingIdentity building,
        SyncRestockPolicyCommand command,
        CancellationToken cancellationToken
    )
    {
        var buildingType = building.BuildingType;
        var workstationId = policy.WorkstationId;

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

        await restockGold.Handle(
            new RestockGoldCommand
            {
                Owner = new ItemOwnerReference(workstationId, OwnerType.Workstation),
                WorldId = policy.WorldId,
                MinimumQuantity = fillResult.GoldMinimum,
            },
            cancellationToken
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

        policy.LastSyncGameTime = command.CurrentGameTime;

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

        var doorsNeedingKeys = guestRoomDoors
            .Where(door =>
                !door.CandidateKeyItemIds.Any(id => workstationIdsByItemId.ContainsKey(id))
            )
            .Select(door => new ReplacementRoomKeyRequest(door.DoorConnectorId, door.RoomName))
            .ToArray();

        await issueReplacementRoomKeys.Handle(
            new IssueReplacementRoomKeysCommand
            {
                WorkstationId = workstationId,
                WorldId = worldId,
                Doors = doorsNeedingKeys,
            },
            cancellationToken
        );
    }
}
