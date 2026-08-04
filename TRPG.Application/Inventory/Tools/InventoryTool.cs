using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Tools;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.Inventory.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Tools;

internal record InventoryItemInfo(string Name, int Quantity);

internal record InventoryResult(
    string OwnerName,
    int Gold,
    IReadOnlyCollection<InventoryItemInfo> Items
);

internal class InventoryTool(
    GameTurnContext turnContext,
    GetCreatureByIdQueryHandler getCreatureById,
    GetCreatureByNameAtLocationQueryHandler getCreatureByNameAtLocation,
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId,
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

        var items = await getInventoryByCreatureId.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = target!.Id },
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
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
