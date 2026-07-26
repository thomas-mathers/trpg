using Spectre.Console;
using TRPG.Client.Core;
using TRPG.Client.Extensions;
using TRPG.Contracts;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Combat.Requests;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Inventory.Responses;

namespace TRPG.Client;

internal sealed class CombatMenu(
    TrpgHttpClient client,
    NarrationRenderer narrationRenderer,
    GameHub gameHub,
    Guid playerId
)
{
    private const string BackLabel = "Back";

    private enum TopLevelOption
    {
        Attack,
        Defend,
        Item,
        Flee,
    }

    private sealed record MenuOption<T>(string Label, T? Value = default);

    public async Task RunTurn(FightState fight, CancellationToken cancellationToken)
    {
        while (true)
        {
            var option = await AnsiConsole.PromptAsync(
                new SelectionPrompt<TopLevelOption>()
                    .Title("Your turn:")
                    .AddChoices(Enum.GetValues<TopLevelOption>()),
                cancellationToken
            );

            switch (option)
            {
                case TopLevelOption.Attack:
                    if (
                        await HandleAbilityMenu(AbilityCategory.Offensive, fight, cancellationToken)
                    )
                    {
                        return;
                    }

                    break;
                case TopLevelOption.Defend:
                    if (await HandleAbilityMenu(AbilityCategory.Support, fight, cancellationToken))
                    {
                        return;
                    }

                    break;
                case TopLevelOption.Item:
                    if (await HandleItemMenu(cancellationToken))
                    {
                        return;
                    }

                    break;
                case TopLevelOption.Flee:
                    await narrationRenderer.TryRender(gameHub.StreamFlee(cancellationToken));
                    return;
            }
        }
    }

    private async Task<bool> HandleItemMenu(CancellationToken cancellationToken)
    {
        var items = await client.GetConsumableItems(playerId, cancellationToken);
        if (items.Count == 0)
        {
            AnsiConsole.AnnounceWarning("No usable items.");
            return false;
        }

        var chosen = await PromptForItem(items, cancellationToken);
        if (chosen == null)
        {
            return false;
        }

        await narrationRenderer.TryRender(
            gameHub.StreamCombatAction(new UseItemAction(chosen.Name), cancellationToken)
        );
        return true;
    }

    private static Task<ConsumableSummary?> PromptForItem(
        IReadOnlyList<ConsumableSummary> candidates,
        CancellationToken cancellationToken
    ) =>
        PromptForOption(
            "Choose an item:",
            candidates,
            i => $"{i.Name} (+{i.RestoreAmount} {i.Resource.ToDisplayName()})",
            cancellationToken
        );

    private async Task<bool> HandleAbilityMenu(
        AbilityCategory category,
        FightState fight,
        CancellationToken cancellationToken
    )
    {
        var abilities = await client.GetAbilities(playerId, cancellationToken);
        var candidates = abilities.Where(a => a.Category == category).ToArray();

        if (candidates.Length == 0)
        {
            AnsiConsole.AnnounceWarning("No abilities of that type.");
            return false;
        }

        while (true)
        {
            var chosen = await PromptForAbility(candidates, cancellationToken);
            if (chosen == null)
            {
                return false;
            }

            if (await ResolveAbility(chosen.Name, chosen.Category, fight, cancellationToken))
            {
                return true;
            }
        }
    }

    private async Task<bool> ResolveAbility(
        string abilityName,
        AbilityCategory category,
        FightState fight,
        CancellationToken cancellationToken
    )
    {
        Guid targetId;

        if (category == AbilityCategory.Support)
        {
            targetId = fight.Combatants.First(c => c.IsPlayer).Id;
        }
        else
        {
            var target = await PromptForTarget(fight, cancellationToken);
            if (target == null)
            {
                return false;
            }

            targetId = target.Value;
        }

        await narrationRenderer.TryRender(
            gameHub.StreamCombatAction(
                new UseAbilityAction(targetId, abilityName),
                cancellationToken
            )
        );
        return true;
    }

    private static Task<AbilitySummary?> PromptForAbility(
        IReadOnlyList<AbilitySummary> candidates,
        CancellationToken cancellationToken
    ) =>
        PromptForOption(
            "Choose an ability:",
            candidates,
            a => $"{a.Name} (AP {a.ApCost}, MP {a.MpCost})",
            cancellationToken
        );

    private static async Task<Guid?> PromptForTarget(
        FightState fight,
        CancellationToken cancellationToken
    )
    {
        var targets = fight
            .Combatants.Where(c => c is { IsPlayer: false, IsAlive: true })
            .ToArray();
        if (targets.Length == 0)
        {
            AnsiConsole.AnnounceWarning("No valid targets.");
            return null;
        }

        var target = await PromptForOption(
            "Target:",
            targets,
            target => target.Name,
            cancellationToken
        );

        return target?.Id;
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
