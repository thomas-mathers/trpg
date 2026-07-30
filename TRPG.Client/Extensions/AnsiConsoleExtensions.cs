using System.Text.RegularExpressions;
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

        public static void PrintCombatStatus(FightState combat, FightState? previous = null)
        {
            AnsiConsole.Write(
                new Padder(
                    new Rule($"[{Theme.Accent}]Combat[/]").RuleStyle(Theme.Neutral).LeftJustified(),
                    new Padding(0, 1)
                )
            );

            var playerLevel = combat.Combatants.First(c => c.IsPlayer).Level;

            // Fixed rather than sized to current content, so panels don't resize turn to turn as
            // effects come and go. +4 covers the panel's default border (1 char each side) and
            // padding (1 char each side).
            AnsiConsole.Write(
                new Columns(
                    combat.Combatants.Select(c =>
                        BuildCombatantPanel(
                            c,
                            previous?.Combatants.FirstOrDefault(p => p.Id == c.Id),
                            playerLevel,
                            PanelContentWidth + 4
                        )
                    )
                )
                {
                    Expand = false,
                }
            );
        }

        private static int VisibleLength(string markup) =>
            MarkupTagPattern.Replace(markup, "").Length;

        private static string Truncate(string text, int maxWidth)
        {
            if (text.Length <= maxWidth)
            {
                return text;
            }

            return maxWidth <= 3 ? text[..maxWidth] : text[..(maxWidth - 3)] + "...";
        }

        // A simple over/under/even split against the player's own level - a quick "is this fight
        // above my weight class" signal, not meant to convey exactly how much harder/easier.
        private static string FormatLevel(int level, int playerLevel)
        {
            var color = level switch
            {
                _ when level > playerLevel => Theme.Negative,
                _ when level < playerLevel => Theme.Positive,
                _ => Theme.Neutral,
            };

            return $"[{color}](Lv {level})[/]";
        }

        private static Panel BuildCombatantPanel(
            CombatantState combatant,
            CombatantState? previous,
            int playerLevel,
            int width
        )
        {
            var hpColor = HealthColor(combatant.CurrentHp, combatant.MaximumHp);
            var nameColor = combatant.IsPlayer ? Theme.PlayerAccent : Theme.Negative;
            var nameLine = combatant.IsPlayer
                ? $"[{nameColor}]{combatant.Name.EscapeMarkup()}[/] [{Theme.Neutral}](you)[/]"
                : $"[{nameColor}]{combatant.Name.EscapeMarkup()}[/] {FormatLevel(combatant.Level, playerLevel)}";

            List<string> lines =
            [
                nameLine,
                BuildResourceLine(
                    "HP",
                    combatant.CurrentHp,
                    combatant.MaximumHp,
                    hpColor,
                    previous?.CurrentHp
                ),
                BuildResourceLine(
                    "AP",
                    combatant.CurrentAp,
                    combatant.MaximumAp,
                    Theme.ApBar,
                    previous?.CurrentAp
                ),
                BuildResourceLine(
                    "MP",
                    combatant.CurrentMp,
                    combatant.MaximumMp,
                    Theme.MpBar,
                    previous?.CurrentMp
                ),
            ];

            lines.AddRange(BuildEffectLines(combatant));

            return new Panel(string.Join("\n", lines))
            {
                Border = BoxBorder.Rounded,
                Expand = false,
                Width = width,
            };
        }

        // The bar stretches to fill whatever width the fixed panel width leaves free after the
        // "XX " tag and the current/maximum/delta text, so it always fills the panel rather than
        // sitting at a fixed size with wasted or overflowing space next to it.
        private static string BuildResourceLine(
            string tag,
            int current,
            int maximum,
            string barColor,
            int? previousCurrent
        )
        {
            var suffix = $"{current}/{maximum}{FormatDelta(current, previousCurrent)}";
            var barWidth = Math.Max(
                MinimumBarWidth,
                PanelContentWidth - tag.Length - 1 - 1 - VisibleLength(suffix)
            );

            return $"{tag} {FormatBar(current, maximum, barColor, barWidth)} {suffix}";
        }

        // No delta shown on the very first render of a fight (previous is null, nothing to
        // compare against yet) or for a combatant that's new to this render (e.g. wasn't in the
        // previous snapshot).
        private static string FormatDelta(int current, int? previous)
        {
            if (previous is null || current == previous.Value)
            {
                return "";
            }

            // No arrow glyph: common Windows terminal fonts cover the Block Elements range (used
            // by the bars above) but not Geometric Shapes, so a triangle silently fails to
            // render. Color plus the signed number already conveys direction unambiguously.
            var delta = current - previous.Value;
            return delta > 0 ? $" [{Theme.Positive}]+{delta}[/]" : $" [{Theme.Negative}]{delta}[/]";
        }

        // One line per effect type, added only when non-empty. Buffs are always positive today -
        // no ability applies a negative stat modifier yet (that'd be a curse, a future feature) -
        // so this doesn't need to handle mixed-sign buffs; revisit if that changes.
        private static List<string> BuildEffectLines(CombatantState combatant)
        {
            List<string> lines = [];

            AddEffectLine(
                lines,
                "Conditions",
                combatant.ActiveConditions.Select(condition =>
                    $"{AbbreviateCondition(condition.Key)} ({condition.Value}t)"
                ),
                Theme.Negative
            );

            AddEffectLine(
                lines,
                "Dots",
                combatant.ActiveDots.Select(dot =>
                    $"-{dot.Amount} {AbbreviateDamageType(dot.DamageType)} ({dot.RemainingTurns}t)"
                ),
                Theme.Negative
            );

            AddEffectLine(
                lines,
                "Hots",
                combatant.ActiveHots.Select(hot => $"+{hot.Amount} HP ({hot.RemainingTurns}t)"),
                Theme.Positive
            );

            AddEffectLine(
                lines,
                "Buffs",
                combatant.ActiveBuffs.Select(buff =>
                    $"{FormatBuffAmount(buff)} {AbbreviateAttribute(buff.Attribute)} ({buff.RemainingTurns}t)"
                ),
                Theme.Positive
            );

            return lines;
        }

        private static void AddEffectLine(
            List<string> lines,
            string label,
            IEnumerable<string> entrySource,
            string color
        )
        {
            var entries = entrySource.ToArray();
            if (entries.Length == 0)
            {
                return;
            }

            var text = string.Join(", ", entries.Take(MaxEffectEntriesPerLine));
            var overflow = entries.Length - MaxEffectEntriesPerLine;
            if (overflow > 0)
            {
                text += $", +{overflow} more";
            }

            // "{label}: " prefix takes label.Length + 2 (colon and space) before text starts.
            text = Truncate(text, PanelContentWidth - label.Length - 2);

            lines.Add($"[{Theme.Neutral}]{label}:[/] [{color}]{text.EscapeMarkup()}[/]");
        }

        private static string FormatBuffAmount(ActiveBuff buff)
        {
            var magnitude =
                buff.AmountType == AmountType.Percent
                    ? $"{buff.Amount:0.#}%"
                    : $"{buff.Amount:0.#}";
            return buff.Amount >= 0 ? $"+{magnitude}" : magnitude;
        }

        // Only ever shown in the tight combat status line above, so abbreviated here rather
        // than in the shared ToDisplayName() extension every other enum in the app relies on.
        private static string AbbreviateAttribute(AttributeName attribute) =>
            attribute switch
            {
                AttributeName.MaximumHp => "HP",
                AttributeName.MaximumAp => "AP",
                AttributeName.MaximumMp => "MP",
                AttributeName.Strength => "STR",
                AttributeName.Defense => "DEF",
                AttributeName.Dexterity => "DEX",
                AttributeName.Endurance => "END",
                AttributeName.Stamina => "STA",
                AttributeName.Mana => "MAN",
                AttributeName.Intelligence => "INT",
                AttributeName.PhysicalResistance => "PR",
                AttributeName.FireResistance => "FR",
                AttributeName.IceResistance => "IR",
                AttributeName.LightningResistance => "LR",
                AttributeName.PoisonResistance => "PoR",
                AttributeName.MagicResistance => "MR",
                AttributeName.MovementSpeed => "SPD",
                _ => attribute.ToDisplayName(),
            };

        // Only ever shown in the tight combat status line above, so abbreviated here rather
        // than in the shared ToDisplayName() extension every other enum in the app relies on.
        private static string AbbreviateCondition(ConditionType condition) =>
            condition switch
            {
                ConditionType.Blinded => "BLI",
                ConditionType.Bleeding => "BLE",
                ConditionType.Burning => "BRN",
                ConditionType.Disarmed => "DIS",
                ConditionType.Frozen => "FRZ",
                ConditionType.Poisoned => "POI",
                ConditionType.Silenced => "SIL",
                ConditionType.Snared => "SNR",
                ConditionType.Stunned => "STN",
                _ => condition.ToDisplayName(),
            };

        // Only ever shown in the tight combat status line above, so abbreviated here rather
        // than in the shared ToDisplayName() extension every other enum in the app relies on.
        private static string AbbreviateDamageType(DamageType damageType) =>
            damageType switch
            {
                DamageType.Physical => "PHY",
                DamageType.Fire => "FIR",
                DamageType.Ice => "ICE",
                DamageType.Lightning => "LGT",
                DamageType.Poison => "PSN",
                DamageType.Magic => "MAG",
                _ => damageType.ToDisplayName(),
            };

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

    private static readonly Regex MarkupTagPattern = new(@"\[[^\]]*\]", RegexOptions.Compiled);
    private const int MaxEffectEntriesPerLine = 3;

    // Rough estimate - fits typical effect lines (label plus a couple of entries) without
    // wrapping, but a combatant stacked with 3 long entries on the longest label ("Conditions:")
    // could overflow. Retune by eye if real content wraps more than expected.
    private const int PanelContentWidth = 42;

    private const int MinimumBarWidth = 5;
}
