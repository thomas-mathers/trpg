using Spectre.Console;
using TRPG.Contracts;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Client.Extensions;

public static class AnsiConsoleExtensions
{
    extension(AnsiConsole)
    {
        public static void Announce(string message) => AnnounceWithColor(Theme.Neutral, message);

        public static void AnnounceSuccess(string message) =>
            AnnounceWithColor(Theme.Positive, message);

        public static void AnnounceWarning(string message) =>
            AnnounceWithColor(Theme.Caution, message);

        public static void AnnounceError(string message) =>
            AnnounceWithColor(Theme.Negative, message);

        public static void PrintTable(IReadOnlyList<string> columns, IEnumerable<string[]> rows)
        {
            var table = new Table().Border(TableBorder.Rounded).ShowRowSeparators();
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

        private static string BuildBreadcrumb(SceneSnapshot scene) =>
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
                >= 0.66f => Theme.Positive,
                >= 0.33f => Theme.Caution,
                _ => Theme.Negative,
            };
        }

        public static string FormatNeutralChip(Enum value) =>
            $"[{Theme.ChipForeground} on {Theme.NeutralAccent}] {value.ToDisplayName().EscapeMarkup()} [/]";

        public static string FormatCreatureChip(Enum value) =>
            $"[{Theme.ChipForeground} on {Theme.CreatureAccent}] {value.ToDisplayName().EscapeMarkup()} [/]";

        public static string FormatBuildingChip(Enum value) =>
            $"[{Theme.ChipForeground} on {Theme.BuildingAccent}] {value.ToDisplayName().EscapeMarkup()} [/]";

        public static string FormatDistrictChip(Enum value) =>
            $"[{Theme.ChipForeground} on {Theme.DistrictAccent}] {value.ToDisplayName().EscapeMarkup()} [/]";

        private static string FormatDebuffChip(string label, int remainingTurns) =>
            $"[{Theme.ChipForeground} on {Theme.Negative}] {label.EscapeMarkup()} · {remainingTurns}t [/]";

        private static string FormatPositiveChip(string label, int remainingTurns) =>
            $"[{Theme.ChipForeground} on {Theme.Positive}] {label.EscapeMarkup()} · {remainingTurns}t [/]";

        public static void PrintCombatStatus(FightState combat)
        {
            AnsiConsole.Write(
                new Padder(
                    new Rule($"[{Theme.Accent}]Combat[/]").RuleStyle(Theme.Neutral).LeftJustified(),
                    new Padding(0, 1)
                )
            );
            AnsiConsole.Write(
                new Columns(combat.Combatants.Select(BuildCombatantPanel)) { Expand = false }
            );
        }

        private static Panel BuildCombatantPanel(CombatantState combatant)
        {
            var hpColor = HealthColor(combatant.CurrentHp, combatant.MaximumHp);
            var nameColor = combatant.IsPlayer ? Theme.PlayerAccent : Theme.Negative;
            var nameLine = combatant.IsPlayer
                ? $"[{nameColor}]{combatant.Name.EscapeMarkup()}[/] [{Theme.Neutral}](you)[/]"
                : $"[{nameColor}]{combatant.Name.EscapeMarkup()}[/]";

            List<string> lines =
            [
                nameLine,
                $"HP {FormatBar(combatant.CurrentHp, combatant.MaximumHp, hpColor, width: 10)} {combatant.CurrentHp}/{combatant.MaximumHp}",
                $"AP {FormatBar(combatant.CurrentAp, combatant.MaximumAp, Theme.ApBar, width: 10)} {combatant.CurrentAp}/{combatant.MaximumAp}",
                $"MP {FormatBar(combatant.CurrentMp, combatant.MaximumMp, Theme.MpBar, width: 10)} {combatant.CurrentMp}/{combatant.MaximumMp}",
            ];

            var effectChips = BuildEffectChips(combatant);
            if (effectChips.Count > 0)
            {
                lines.Add(string.Join(" ", effectChips));
            }

            return new Panel(string.Join("\n", lines))
            {
                Border = BoxBorder.Rounded,
                Expand = false,
            };
        }

        private static List<string> BuildEffectChips(CombatantState combatant)
        {
            var chips = new List<string>();

            chips.AddRange(
                combatant.ActiveConditions.Select(condition =>
                    FormatDebuffChip(condition.Key.ToDisplayName(), condition.Value)
                )
            );

            chips.AddRange(
                combatant.ActiveDots.Select(dot =>
                    FormatDebuffChip(
                        $"{dot.AbilityName} · {dot.Amount} {dot.DamageType.ToDisplayName()}",
                        dot.RemainingTurns
                    )
                )
            );

            chips.AddRange(
                combatant.ActiveHots.Select(hot =>
                    FormatPositiveChip($"{hot.AbilityName} +{hot.Amount}/turn", hot.RemainingTurns)
                )
            );

            chips.AddRange(
                combatant.ActiveBuffs.Select(buff =>
                {
                    var label =
                        $"{buff.AbilityName} · {buff.Attribute.ToDisplayName()} {FormatBuffAmount(buff)}";
                    return buff.Amount >= 0
                        ? FormatPositiveChip(label, buff.RemainingTurns)
                        : FormatDebuffChip(label, buff.RemainingTurns);
                })
            );

            return chips;
        }

        private static string FormatBuffAmount(ActiveBuff buff)
        {
            var magnitude =
                buff.AmountType == AmountType.Percent
                    ? $"{buff.Amount:0.#}%"
                    : $"{buff.Amount:0.#}";
            return buff.Amount >= 0 ? $"+{magnitude}" : magnitude;
        }

        private static void AnnounceWithColor(string color, string message)
        {
            AnsiConsole.Write(new Padder(new Markup($"[{color}]{message}[/]"), new Padding(0, 1)));
        }

        public static void PrintStatus(SceneSnapshot scene)
        {
            var title =
                $"{AnsiConsole.BuildBreadcrumb(scene).EscapeMarkup()} | {scene.WeekdayName.EscapeMarkup()}, Hour {scene.Hour}";

            AnsiConsole.Write(
                new Padder(
                    new Rule($"[{Theme.Neutral}]{title}[/]")
                        .RuleStyle(Theme.Neutral)
                        .LeftJustified(),
                    new Padding(0, 1)
                )
            );
        }
    }
}
