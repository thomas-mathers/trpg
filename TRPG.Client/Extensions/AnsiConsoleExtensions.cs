using System.CommandLine;
using Spectre.Console;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Client.Extensions;

public static class AnsiConsoleExtensions
{
    private static readonly IReadOnlyDictionary<string, string> StateChipColors = new Dictionary<
        string,
        string
    >(StringComparer.OrdinalIgnoreCase)
    {
        ["Sleeping"] = "cyan1",
        ["Idle"] = "khaki1",
        ["Busy"] = "orange3",
        ["Studying"] = "turquoise2",
        ["Praying"] = "wheat1",
        ["Training"] = "deeppink2",
        ["Sitting"] = "plum2",
        ["Dead"] = "grey50",
    };

    extension(AnsiConsole)
    {
        public static void Announce(string message) => AnnounceWithColor("grey", message);

        public static void AnnounceSuccess(string message) => AnnounceWithColor("green", message);

        public static void AnnounceWarning(string message) => AnnounceWithColor("yellow", message);

        public static void AnnounceError(string message) => AnnounceWithColor("red", message);

        public static void RenderTable(IReadOnlyList<string> columns, IEnumerable<string[]> rows)
        {
            var table = new Table().Border(TableBorder.Rounded);
            foreach (var column in columns)
            {
                table.AddColumn(column);
            }

            foreach (var row in rows)
            {
                table.AddRow(row);
            }

            AnsiConsole.Write(table);
        }

        public static string FormatReputation(int reputation)
        {
            var color = reputation switch
            {
                > 0 => "green",
                < 0 => "red",
                _ => "grey",
            };
            return $"[{color}]{reputation}[/]";
        }

        public static string FormatStatusBars(
            int currentHp,
            int maximumHp,
            int currentAp,
            int maximumAp,
            int currentMp,
            int maximumMp
        )
        {
            var hpColor = HealthColor(currentHp, maximumHp);
            return string.Join(
                "\n",
                $"HP {FormatBar(currentHp, maximumHp, hpColor, width: 8)} {currentHp}/{maximumHp}",
                $"AP {FormatBar(currentAp, maximumAp, "blue", width: 8)} {currentAp}/{maximumAp}",
                $"MP {FormatBar(currentMp, maximumMp, "purple", width: 8)} {currentMp}/{maximumMp}"
            );
        }

        public static string BuildBreadcrumb(SceneSnapshot scene) =>
            string.Join(
                " > ",
                new[]
                {
                    scene.StateName,
                    scene.CityName,
                    scene.DistrictName,
                    scene.BuildingName,
                    scene.RoomName,
                }.Where(name => !string.IsNullOrEmpty(name))
            );

        public static string FormatBar(int current, int maximum, string color, int width = 20)
        {
            var filled =
                maximum > 0
                    ? Math.Clamp((int)Math.Round(width * (current / (float)maximum)), 0, width)
                    : 0;
            var bar = new string('█', filled) + new string('░', width - filled);
            return $"[{color}]{bar}[/]";
        }

        public static string HealthColor(int currentHp, int maximumHp)
        {
            var percentage = maximumHp > 0 ? currentHp / (float)maximumHp : 0f;
            return percentage switch
            {
                >= 0.66f => "green",
                >= 0.33f => "yellow",
                _ => "red",
            };
        }

        public static string FormatStateChip(string value) => FormatChip(value, StateChipColors);

        private static string FormatChip(string value, IReadOnlyDictionary<string, string> colorsByValue)
        {
            var color = colorsByValue.GetValueOrDefault(value, "grey70");
            return $"[{color} on grey19] {value.EscapeMarkup()} [/]";
        }

        public static string FormatNeutralChip(string value) =>
            $"[grey70 on grey19] {value.EscapeMarkup()} [/]";

        public static string FormatDebuffChip(string label, int remainingTurns) =>
            $"[red on grey19] {label.EscapeMarkup()} · {remainingTurns}t [/]";

        public static string FormatBuffChip(string label, int remainingTurns) =>
            $"[blue on grey19] {label.EscapeMarkup()} · {remainingTurns}t [/]";

        public static string FormatHotChip(string label, int remainingTurns) =>
            $"[green on grey19] {label.EscapeMarkup()} · {remainingTurns}t [/]";

        public static void RenderCombatStatus(CombatSnapshot? combat)
        {
            if (combat == null)
            {
                return;
            }

            AnsiConsole.Write(
                new Padder(
                    new Rule("[gold1]Combat[/]").RuleStyle("grey").LeftJustified(),
                    new Padding(0, 1)
                )
            );
            AnsiConsole.Write(
                new Columns(combat.Combatants.Select(BuildCombatantPanel)) { Expand = false }
            );
        }

        private static Panel BuildCombatantPanel(CombatantStatusSnapshot combatant)
        {
            var hpColor = HealthColor(combatant.CurrentHp, combatant.MaximumHp);
            var nameColor = combatant.IsPlayer ? "dodgerblue1" : "red";
            var nameLine = combatant.IsPlayer
                ? $"[{nameColor}]{combatant.Name.EscapeMarkup()}[/] [grey](you)[/]"
                : $"[{nameColor}]{combatant.Name.EscapeMarkup()}[/]";

            List<string> lines =
            [
                nameLine,
                $"HP {FormatBar(combatant.CurrentHp, combatant.MaximumHp, hpColor, width: 10)} {combatant.CurrentHp}/{combatant.MaximumHp}",
                $"AP {FormatBar(combatant.CurrentAp, combatant.MaximumAp, "blue", width: 10)} {combatant.CurrentAp}/{combatant.MaximumAp}",
                $"MP {FormatBar(combatant.CurrentMp, combatant.MaximumMp, "purple", width: 10)} {combatant.CurrentMp}/{combatant.MaximumMp}",
            ];

            var effectChips = BuildEffectChips(combatant);
            if (effectChips.Count > 0)
            {
                lines.Add(string.Join(" ", effectChips));
            }

            return new Panel(string.Join("\n", lines)) { Border = BoxBorder.Rounded, Expand = false };
        }

        private static List<string> BuildEffectChips(CombatantStatusSnapshot combatant)
        {
            var chips = new List<string>();

            chips.AddRange(
                combatant.ActiveConditions.Select(condition =>
                    FormatDebuffChip(condition.Key, condition.Value)
                )
            );

            chips.AddRange(
                combatant.ActiveDots.Select(dot =>
                    FormatDebuffChip(
                        $"{dot.AbilityName} · {dot.Amount} {dot.DamageType}",
                        dot.RemainingTurns
                    )
                )
            );

            chips.AddRange(
                combatant.ActiveHots.Select(hot =>
                    FormatHotChip($"{hot.AbilityName} +{hot.Amount}/turn", hot.RemainingTurns)
                )
            );

            chips.AddRange(
                combatant.ActiveBuffs.Select(buff =>
                {
                    var label = $"{buff.Attribute} {FormatBuffAmount(buff)}";
                    return buff.Amount >= 0
                        ? FormatBuffChip(label, buff.RemainingTurns)
                        : FormatDebuffChip(label, buff.RemainingTurns);
                })
            );

            return chips;
        }

        private static string FormatBuffAmount(ActiveBuffStatus buff)
        {
            var magnitude = buff.AmountType.Equals("Percent", StringComparison.OrdinalIgnoreCase)
                ? $"{buff.Amount:0.#}%"
                : $"{buff.Amount:0.#}";
            return buff.Amount >= 0 ? $"+{magnitude}" : magnitude;
        }

        public static string FormatSyntax(Command command)
        {
            var argumentsText = string.Concat(
                command.Arguments.Select(a => $" [grey italic]<{a.Name}>[/]")
            );
            return $"[bold #569CD6]/{command.Name}[/]{argumentsText}";
        }

        private static void AnnounceWithColor(string color, string message)
        {
            AnsiConsole.Write(new Padder(new Markup($"[{color}]{message}[/]"), new Padding(0, 1)));
        }
    }
}
