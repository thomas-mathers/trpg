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
        if (inventory.Items.Count == 0)
        {
            AnsiConsole.AnnounceWarning($"{corpse.Name} has nothing left to loot.");
            return;
        }

        var selection = await PromptForSelection(inventory, cancellationToken);
        if (selection.Count == 0)
        {
            return;
        }

        await client.InventoryTransfer(
            corpse.Id,
            playerId,
            new InventoryTransferRequest(selection),
            cancellationToken
        );

        AnsiConsole.AnnounceSuccess(DescribeLoot(selection, inventory.Items));
    }

    private static Task<NearbyCorpseSummary?> PromptForCorpse(
        IReadOnlyList<NearbyCorpseSummary> candidates,
        CancellationToken cancellationToken
    ) =>
        PromptForOption(
            "Loot which corpse?",
            candidates,
            c => $"{c.Name} ({c.ItemCount})",
            cancellationToken
        );

    private static async Task<IReadOnlyList<LootItemSelection>> PromptForSelection(
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
            return inventory
                .Items.Select(i => new LootItemSelection(i.ItemId, i.Quantity))
                .ToArray();
        }

        var chosen = await AnsiConsole.PromptAsync(
            new MultiSelectionPrompt<ItemSummary>()
                .Title("Choose what to take:")
                .NotRequired()
                .UseConverter(i => $"{i.Name} x{i.Quantity}")
                .AddChoices(inventory.Items),
            cancellationToken
        );

        var selections = new List<LootItemSelection>();
        foreach (var item in chosen)
        {
            var quantity =
                item.Quantity > 1
                    ? await PromptForQuantity(item, cancellationToken)
                    : item.Quantity;
            selections.Add(new LootItemSelection(item.ItemId, quantity));
        }

        return selections;
    }

    private static Task<int> PromptForQuantity(
        ItemSummary item,
        CancellationToken cancellationToken
    ) =>
        AnsiConsole.PromptAsync(
            new TextPrompt<int>(
                $"How many {item.Name.EscapeMarkup()} to take? (1-{item.Quantity})"
            ).Validate(quantity =>
                quantity switch
                {
                    < 1 => ValidationResult.Error("Must take at least 1."),
                    _ when quantity > item.Quantity => ValidationResult.Error(
                        $"Only {item.Quantity} available."
                    ),
                    _ => ValidationResult.Success(),
                }
            ),
            cancellationToken
        );

    private static string DescribeLoot(
        IReadOnlyCollection<LootItemSelection> takenItems,
        IReadOnlyList<ItemSummary> availableItems
    )
    {
        var parts = takenItems
            .Select(taken =>
            {
                var item = availableItems.First(i => i.ItemId == taken.ItemId);
                return $"{item.Name} x{taken.Quantity}";
            })
            .ToArray();

        return parts.Length > 0 ? $"You looted: {string.Join(", ", parts)}." : "You took nothing.";
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
