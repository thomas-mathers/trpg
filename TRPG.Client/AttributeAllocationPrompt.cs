using System.Globalization;
using Spectre.Console;
using TRPG.Client.Extensions;
using TRPG.Contracts;
using TRPG.Contracts.Creatures.Requests;
using TRPG.Contracts.Creatures.Responses;

namespace TRPG.Client;

internal static class AttributeAllocationPrompt
{
    private static readonly AllocatableAttributeName[] AllocatableAttributes =
        Enum.GetValues<AllocatableAttributeName>();

    private enum MenuAction
    {
        Increase,
        Decrease,
        Confirm,
        Cancel,
    }

    private sealed record MenuOption(
        string Label,
        MenuAction Action,
        AllocatableAttributeName? Attribute = null
    );

    public static Task<IReadOnlyDictionary<AllocatableAttributeName, int>?> Run(
        int totalPoints,
        BaseAttributesResponse baseAttributes,
        CancellationToken cancellationToken
    ) =>
        RunLoop(
            totalPoints,
            baseAttributes,
            canDecrease: (_, delta) => delta > 0,
            cancellationToken
        );

    public static Task<IReadOnlyDictionary<AllocatableAttributeName, int>?> RunAtCreation(
        int totalPoints,
        BaseAttributesResponse baseAttributes,
        CancellationToken cancellationToken
    ) =>
        RunLoop(
            totalPoints,
            baseAttributes,
            canDecrease: (current, delta) => current + delta - 1 >= 1,
            cancellationToken
        );

    private static async Task<IReadOnlyDictionary<AllocatableAttributeName, int>?> RunLoop(
        int totalPoints,
        BaseAttributesResponse baseAttributes,
        Func<int, int, bool> canDecrease,
        CancellationToken cancellationToken
    )
    {
        var deltas = new Dictionary<AllocatableAttributeName, int>();
        var remaining = totalPoints;

        while (true)
        {
            PrintAllocation(baseAttributes, deltas, remaining);

            var chosen = await AnsiConsole.PromptAsync(
                new SelectionPrompt<MenuOption>()
                    .Title("Choose an action:")
                    .UseConverter(o => o.Label)
                    .AddChoices(BuildChoices(baseAttributes, deltas, remaining, canDecrease)),
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
                    var updated = deltas.GetValueOrDefault(chosen.Attribute!.Value) - 1;
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
        IReadOnlyDictionary<AllocatableAttributeName, int> deltas,
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
                    delta switch
                    {
                        0 => "",
                        > 0 => $"+{delta}",
                        _ => delta.ToString(CultureInfo.InvariantCulture),
                    },
                    (current + delta).ToString(CultureInfo.InvariantCulture),
                };
            })
        );
        AnsiConsole.AnnounceSuccess($"Points remaining: {remaining}");
    }

    private static int GetValue(
        BaseAttributesResponse baseAttributes,
        AllocatableAttributeName attribute
    ) =>
        attribute switch
        {
            AllocatableAttributeName.Strength => baseAttributes.Strength,
            AllocatableAttributeName.Dexterity => baseAttributes.Dexterity,
            AllocatableAttributeName.Endurance => baseAttributes.Endurance,
            AllocatableAttributeName.Stamina => baseAttributes.Stamina,
            AllocatableAttributeName.Mana => baseAttributes.Mana,
            AllocatableAttributeName.Intelligence => baseAttributes.Intelligence,
            _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null),
        };

    private static IReadOnlyList<MenuOption> BuildChoices(
        BaseAttributesResponse baseAttributes,
        IReadOnlyDictionary<AllocatableAttributeName, int> deltas,
        int remaining,
        Func<int, int, bool> canDecrease
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
                .Where(a => canDecrease(GetValue(baseAttributes, a), deltas.GetValueOrDefault(a)))
                .Select(a => new MenuOption($"-1 {a.ToDisplayName()}", MenuAction.Decrease, a))
        );

        choices.Add(new MenuOption("Confirm allocation", MenuAction.Confirm));
        choices.Add(new MenuOption("Cancel", MenuAction.Cancel));

        return choices;
    }
}
