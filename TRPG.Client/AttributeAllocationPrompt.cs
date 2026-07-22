using System.Globalization;
using Spectre.Console;
using TRPG.Client.Extensions;
using TRPG.Contracts;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Creatures.Responses;

namespace TRPG.Client;

internal static class AttributeAllocationPrompt
{
    private static readonly AttributeName[] AllocatableAttributes =
    [
        AttributeName.Strength,
        AttributeName.Dexterity,
        AttributeName.Endurance,
        AttributeName.Stamina,
        AttributeName.Mana,
        AttributeName.Intelligence,
    ];

    private enum MenuAction
    {
        Increase,
        Decrease,
        Confirm,
        Cancel,
    }

    private sealed record MenuOption(string Label, MenuAction Action, AttributeName? Attribute = null);

    public static async Task<IReadOnlyDictionary<AttributeName, int>?> Run(
        int totalPoints,
        BaseAttributesResponse baseAttributes,
        CancellationToken cancellationToken
    )
    {
        var deltas = new Dictionary<AttributeName, int>();
        var remaining = totalPoints;

        while (true)
        {
            PrintAllocation(baseAttributes, deltas, remaining);

            var chosen = await AnsiConsole.PromptAsync(
                new SelectionPrompt<MenuOption>()
                    .Title("Choose an action:")
                    .UseConverter(o => o.Label)
                    .AddChoices(BuildChoices(deltas, remaining)),
                cancellationToken
            );

            switch (chosen.Action)
            {
                case MenuAction.Cancel:
                    return null;
                case MenuAction.Confirm:
                    return deltas;
                case MenuAction.Increase:
                    deltas[chosen.Attribute!.Value] =
                        deltas.GetValueOrDefault(chosen.Attribute.Value) + 1;
                    remaining--;
                    break;
                case MenuAction.Decrease:
                    var updated = deltas[chosen.Attribute!.Value] - 1;
                    if (updated == 0)
                    {
                        deltas.Remove(chosen.Attribute.Value);
                    }
                    else
                    {
                        deltas[chosen.Attribute.Value] = updated;
                    }

                    remaining++;
                    break;
            }
        }
    }

    private static void PrintAllocation(
        BaseAttributesResponse baseAttributes,
        IReadOnlyDictionary<AttributeName, int> deltas,
        int remaining
    )
    {
        AnsiConsole.PrintTable(
            ["Attribute", "Current", "Allocated", "New"],
            AllocatableAttributes.Select(a =>
            {
                var current = GetValue(baseAttributes, a);
                var delta = deltas.GetValueOrDefault(a);
                return new[]
                {
                    a.ToDisplayName(),
                    current.ToString(CultureInfo.InvariantCulture),
                    delta == 0 ? "" : $"+{delta}",
                    (current + delta).ToString(CultureInfo.InvariantCulture),
                };
            })
        );
        AnsiConsole.AnnounceSuccess($"Points remaining: {remaining}");
    }

    private static int GetValue(BaseAttributesResponse baseAttributes, AttributeName attribute) =>
        attribute switch
        {
            AttributeName.Strength => baseAttributes.Strength,
            AttributeName.Dexterity => baseAttributes.Dexterity,
            AttributeName.Endurance => baseAttributes.Endurance,
            AttributeName.Stamina => baseAttributes.Stamina,
            AttributeName.Mana => baseAttributes.Mana,
            AttributeName.Intelligence => baseAttributes.Intelligence,
            _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null),
        };

    private static IReadOnlyList<MenuOption> BuildChoices(
        IReadOnlyDictionary<AttributeName, int> deltas,
        int remaining
    )
    {
        var choices = new List<MenuOption>();

        if (remaining > 0)
        {
            choices.AddRange(
                AllocatableAttributes.Select(a => new MenuOption(
                    $"+1 {a.ToDisplayName()}",
                    MenuAction.Increase,
                    a
                ))
            );
        }

        choices.AddRange(
            AllocatableAttributes
                .Where(deltas.ContainsKey)
                .Select(a => new MenuOption($"-1 {a.ToDisplayName()}", MenuAction.Decrease, a))
        );

        choices.Add(new MenuOption("Confirm allocation", MenuAction.Confirm));
        choices.Add(new MenuOption("Cancel", MenuAction.Cancel));

        return choices;
    }
}
