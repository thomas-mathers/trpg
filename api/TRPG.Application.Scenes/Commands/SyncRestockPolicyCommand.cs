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

        policy.LastSyncPlaytime = command.CurrentPlaytime;

        await context.SaveChangesAsync(cancellationToken);
    }
}
