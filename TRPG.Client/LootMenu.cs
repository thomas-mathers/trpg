using Spectre.Console;
using TRPG.Client.Extensions;
using TRPG.Contracts.Creatures.Responses;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Contracts.Inventory.Responses;

namespace TRPG.Client;

internal sealed class LootMenu(TrpgHttpClient client, Guid playerId)
{
    private const string BackLabel = "Back";

    private enum TakeMode
    {
        TakeEverything,
        ChooseWhatToTake,
    }

    private sealed record MenuOption<T>(string Label, T? Value = default);

    private sealed record LootChoice(string Label, bool IsGold, InventoryItemSummary? Item);

    private sealed record LootSelection(bool TakeGold, IReadOnlyList<LootItemSelection> Items);

    public async Task Run(CancellationToken cancellationToken)
    {
        var corpses = await client.GetNearbyCorpses(playerId, cancellationToken);
        if (corpses.Count == 0)
        {
            AnsiConsole.AnnounceWarning("Nothing to loot here.");
            return;
        }

        var corpse = await PromptForCorpse(corpses, cancellationToken);
        if (corpse == null)
        {
            return;
        }

        var inventory = await client.GetInventory(corpse.Id, cancellationToken);
        if (inventory.Gold == 0 && inventory.Items.Count == 0)
        {
            AnsiConsole.AnnounceWarning($"{corpse.Name} has nothing left to loot.");
            return;
        }

        var selection = await PromptForSelection(inventory, cancellationToken);
        if (!selection.TakeGold && selection.Items.Count == 0)
        {
            return;
        }

        await client.InventoryTransfer(
            corpse.Id,
            playerId,
            new InventoryTransferRequest(selection.TakeGold, selection.Items),
            cancellationToken
        );

        AnsiConsole.AnnounceSuccess(
            DescribeLoot(selection.TakeGold, inventory.Gold, selection.Items, inventory.Items)
        );
    }

    private static Task<NearbyCorpseSummary?> PromptForCorpse(
        IReadOnlyList<NearbyCorpseSummary> candidates,
        CancellationToken cancellationToken
    ) => PromptForOption("Loot which corpse?", candidates, c => c.Name, cancellationToken);

    private static async Task<LootSelection> PromptForSelection(
        InventorySummary inventory,
        CancellationToken cancellationToken
    )
    {
        var mode = await AnsiConsole.PromptAsync(
            new SelectionPrompt<TakeMode>()
                .Title("Take what?")
                .UseConverter(m =>
                    m == TakeMode.TakeEverything ? "Take everything" : "Choose what to take"
                )
                .AddChoices(Enum.GetValues<TakeMode>()),
            cancellationToken
        );

        if (mode == TakeMode.TakeEverything)
        {
            return new LootSelection(
                inventory.Gold > 0,
                inventory.Items.Select(i => new LootItemSelection(i.ItemId, i.Quantity)).ToArray()
            );
        }

        var choices = new List<LootChoice>();
        if (inventory.Gold > 0)
        {
            choices.Add(new LootChoice($"Gold ({inventory.Gold})", IsGold: true, Item: null));
        }

        choices.AddRange(
            inventory.Items.Select(i => new LootChoice(
                $"{i.Name} x{i.Quantity}",
                IsGold: false,
                Item: i
            ))
        );

        var chosen = await AnsiConsole.PromptAsync(
            new MultiSelectionPrompt<LootChoice>()
                .Title("Choose what to take:")
                .NotRequired()
                .UseConverter(c => c.Label)
                .AddChoices(choices),
            cancellationToken
        );

        var takeGold = chosen.Any(c => c.IsGold);
        var items = chosen
            .Where(c => c.Item != null)
            .Select(c => new LootItemSelection(c.Item!.ItemId, c.Item.Quantity))
            .ToArray();

        return new LootSelection(takeGold, items);
    }

    private static string DescribeLoot(
        bool takeGold,
        int gold,
        IReadOnlyCollection<LootItemSelection> takenItems,
        IReadOnlyList<InventoryItemSummary> availableItems
    )
    {
        var parts = new List<string>();
        if (takeGold)
        {
            parts.Add($"{gold} gold");
        }

        parts.AddRange(
            takenItems.Select(taken =>
            {
                var item = availableItems.First(i => i.ItemId == taken.ItemId);
                return $"{item.Name} x{taken.Quantity}";
            })
        );

        return parts.Count > 0 ? $"You looted: {string.Join(", ", parts)}." : "You took nothing.";
    }

    private static async Task<T?> PromptForOption<T>(
        string title,
        IReadOnlyList<T> candidates,
        Func<T, string> formatLabel,
        CancellationToken cancellationToken
    )
    {
        var choices = candidates
            .Select(item => new MenuOption<T>(formatLabel(item), item))
            .Append(new MenuOption<T>(BackLabel))
            .ToArray();

        var chosen = await AnsiConsole.PromptAsync(
            new SelectionPrompt<MenuOption<T>>()
                .Title(title)
                .UseConverter(c => c.Label)
                .AddChoices(choices),
            cancellationToken
        );

        return chosen.Value;
    }
}
