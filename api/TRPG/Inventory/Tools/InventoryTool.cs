using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Domain.Models;
using TRPG.Tools;

namespace TRPG.Inventory.Tools;

internal record InventoryItemInfo(string Name, int Quantity);

internal record InventoryResult(
    string OwnerName,
    int Gold,
    IReadOnlyCollection<InventoryItemInfo> Items
);

internal class InventoryTool(
    GameTurnContext turnContext,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?> getCreatureByNameAtLocation,
    IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>> getInventoryItemsByOwner,
    ILogger<InventoryTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("inventory")]
    [Description(
        "Returns the items someone is carrying. Omit targetName to check the player's own inventory, or pass the exact Name of a person from NearbyPeople to check theirs."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of a person from NearbyPeople, copied verbatim from the most recent look or move result. Omit to check the player's own inventory."
        )]
            string? targetName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[inventory] targetName={TargetName}", targetName ?? "(self)");
        var stopwatch = Stopwatch.StartNew();

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );

        Creature? target;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            target = player;
        }
        else
        {
            target = await getCreatureByNameAtLocation.Handle(
                new GetCreatureByNameAtLocationQuery
                {
                    WorldId = turnContext.WorldId,
                    LocationId = player!.LocationId,
                    Name = targetName,
                },
                cancellationToken
            );

            if (target == null)
            {
                return new ToolError(
                    $"No one named '{targetName}' found nearby. Call look to see who's around."
                );
            }
        }

        var items = await getInventoryItemsByOwner.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(target!.Id, OwnerType.Creature),
            },
            cancellationToken
        );

        var result = new InventoryResult(
            target.Name,
            items.OfType<Gold>().Sum(i => i.Quantity),
            items.Select(i => new InventoryItemInfo(i.Name, i.Quantity)).ToArray()
        );

        logger.LogInformation(
            "[perf] [inventory] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(
                result,
                TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
            )
        );
        return result;
    }
}
