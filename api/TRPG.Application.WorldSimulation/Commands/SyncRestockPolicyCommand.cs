using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Scheduling;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Commands;

public class SyncRestockPolicyCommand
{
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
    public required GameInstant CurrentGameTime { get; init; }
}

internal class SyncRestockPolicyCommandHandler(
    IWorldSimulationDbContext context,
    ItemGenerator itemGenerator,
    IQueryHandler<
        GetWorkstationsByLocationIdQuery,
        IReadOnlyCollection<Workstation>
    > getWorkstationsByLocationId,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>> getInventoryItemsByOwner,
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<RestockGoldCommand> restockGold,
    ICommandHandler<UpdateItemQuantitiesCommand> updateItemQuantities,
    IDomainEventPublisher<WorkstationRestockedEvent> workstationRestockedPublisher
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
        await Restock(policy, building.BuildingType, command, cancellationToken);

        await workstationRestockedPublisher.Publish(
            new WorkstationRestockedEvent(
                policy.WorldId,
                building.Id,
                policy.WorkstationId,
                command.CurrentGameTime
            ),
            cancellationToken
        );
    }

    private async Task Restock(
        RestockPolicy policy,
        BuildingType buildingType,
        SyncRestockPolicyCommand command,
        CancellationToken cancellationToken
    )
    {
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

        policy.LastSyncGameTime = command.CurrentGameTime;

        await context.SaveChangesAsync(cancellationToken);

        transaction.Complete();
    }
}
