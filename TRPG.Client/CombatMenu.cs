using Spectre.Console;
using TRPG.Client.Extensions;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Combat.Responses;

namespace TRPG.Client;

internal sealed class CombatMenu(
    GameServerClient client,
    NarrationRenderer narrationRenderer,
    GameHub gameHub,
    Guid worldId
)
{
    private const string BackLabel = "Back";

    private enum TopLevelOption
    {
        Attack,
        Defend,
        Flee,
    }

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
                    if (await HandleAbilityMenu(AbilityCategory.Offensive, fight, cancellationToken))
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
                case TopLevelOption.Flee:
                    await narrationRenderer.TryRender(gameHub.StreamFlee(cancellationToken));
                    return;
            }
        }
    }

    private async Task<bool> HandleAbilityMenu(
        AbilityCategory category,
        FightState fight,
        CancellationToken cancellationToken
    )
    {
        var abilities = await client.GetAbilities(worldId, cancellationToken);
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
        string targetName;
        if (category == AbilityCategory.Support)
        {
            targetName = fight.Combatants.First(c => c.IsPlayer).Name;
        }
        else
        {
            var target = await PromptForTarget(fight, cancellationToken);
            if (target == null)
            {
                return false;
            }

            targetName = target;
        }

        await narrationRenderer.TryRender(
            gameHub.StreamCombatAction(abilityName, targetName, cancellationToken)
        );
        return true;
    }

    private static async Task<AbilitySummary?> PromptForAbility(
        IReadOnlyList<AbilitySummary> candidates,
        CancellationToken cancellationToken
    )
    {
        var choices = candidates
            .Select(a => (Ability: (AbilitySummary?)a, Label: $"{a.Name} (AP {a.ApCost}, MP {a.MpCost})"))
            .Append((Ability: (AbilitySummary?)null, Label: BackLabel))
            .ToArray();

        var chosen = await AnsiConsole.PromptAsync(
            new SelectionPrompt<(AbilitySummary? Ability, string Label)>()
                .Title("Choose an ability:")
                .UseConverter(c => c.Label)
                .AddChoices(choices),
            cancellationToken
        );

        return chosen.Ability;
    }

    private static async Task<string?> PromptForTarget(
        FightState fight,
        CancellationToken cancellationToken
    )
    {
        var targets = fight.Combatants.Where(c => !c.IsPlayer && c.IsAlive).ToArray();
        if (targets.Length == 0)
        {
            AnsiConsole.AnnounceWarning("No valid targets.");
            return null;
        }

        var choices = targets
            .Select(c => (Name: (string?)c.Name, Label: c.Name))
            .Append((Name: (string?)null, Label: BackLabel))
            .ToArray();

        var chosen = await AnsiConsole.PromptAsync(
            new SelectionPrompt<(string? Name, string Label)>()
                .Title("Target:")
                .UseConverter(c => c.Label)
                .AddChoices(choices),
            cancellationToken
        );

        return chosen.Name;
    }
}
