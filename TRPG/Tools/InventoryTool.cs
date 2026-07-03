using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Models;
using TRPG.Services;

namespace TRPG.Tools;

internal record InventoryItemInfo(string Name, int Quantity);

internal record InventoryResult(string OwnerName, int Gold, IReadOnlyCollection<InventoryItemInfo> Items);

internal class InventoryTool : Tool, IInvokableTool {
    private readonly GameSession _session;
    private readonly PersonService _personService;
    private readonly InventoryService _inventoryService;
    private readonly ILogger<InventoryTool> _logger;

    public InventoryTool(GameSession session, PersonService personService, InventoryService inventoryService, ILogger<InventoryTool> logger) {
        _session = session;
        _personService = personService;
        _inventoryService = inventoryService;
        _logger = logger;

        Function = new Function {
            Name = "inventory",
            Description = "Returns the items someone is carrying. Omit targetName to check the player's own inventory, or pass the exact Name of a person from NearbyPeople to check theirs.",
            Parameters = new Parameters {
                Type = "object",
                Properties = new Dictionary<string, Property> {
                    ["targetName"] = new() {
                        Type = "string",
                        Description = "The exact Name of a person from NearbyPeople, copied verbatim from the most recent look or move result. Omit to check the player's own inventory."
                    }
                },
                Required = new List<string>()
            }
        };
    }

    private async Task<object?> InvokeMethodAsync(string? targetName, CancellationToken cancellationToken) {
        var player = await _personService.GetById(_session.PlayerId, cancellationToken);

        Person? target;
        if (string.IsNullOrWhiteSpace(targetName)) {
            target = player;
        } else {
            target = await _personService.GetByNameNearby(_session.WorldId, player!, targetName, cancellationToken);

            if (target == null) {
                return new { Error = $"No one named '{targetName}' found nearby. Call look to see who's around." };
            }
        }

        var items = await _inventoryService.GetAllByPersonId(target!.Id, cancellationToken);

        return new InventoryResult(
            target.Name,
            target.Gold,
            items.Select(i => new InventoryItemInfo(i.Item.Name, i.Quantity)).ToArray()
        );
    }

    public object? InvokeMethod(IDictionary<string, object?>? args) {
        var targetName = args != null && args.TryGetValue("targetName", out var raw) ? raw?.ToString() : null;

        _logger.LogDebug("[inventory] targetName={TargetName}", targetName ?? "(self)");
        var result = InvokeMethodAsync(targetName, CancellationToken.None).GetAwaiter().GetResult();
        var json = System.Text.Json.JsonSerializer.Serialize(result, ToolJsonOptions.Options);
        _logger.LogDebug("[inventory] result: {Result}", json);
        return json;
    }
}
