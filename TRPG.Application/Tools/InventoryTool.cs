using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Inventory.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Tools;

internal record InventoryItemInfo(string Name, int Quantity);

internal record InventoryResult(
    string OwnerName,
    int Gold,
    IReadOnlyCollection<InventoryItemInfo> Items
);

internal class InventoryTool(
    GameSession session,
    GetCreatureByIdQueryHandler getCreatureById,
    GetCreatureByNameNearbyQueryHandler getCreatureByNameNearby,
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
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );

        Creature? target;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            target = player;
        }
        else
        {
            target = await getCreatureByNameNearby.Handle(
                new GetCreatureByNameNearbyQuery
                {
                    WorldId = session.WorldId,
                    Player = player!,
                    Name = targetName,
                },
                cancellationToken
            );

            if (target == null)
            {
                return new
                {
                    Error = $"No one named '{targetName}' found nearby. Call look to see who's around.",
                };
            }
        }

        var items = await getInventoryByCreatureId.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = target!.Id },
            cancellationToken
        );

        var result = new InventoryResult(
            target.Name,
            target.Gold,
            items.Select(i => new InventoryItemInfo(i.Item.Name, i.Quantity)).ToArray()
        );

        logger.LogInformation(
            "[perf] [inventory] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
