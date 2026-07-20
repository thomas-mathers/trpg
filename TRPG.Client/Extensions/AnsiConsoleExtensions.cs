using System.CommandLine;
using Spectre.Console;
using TRPG.Contracts;
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

    private static readonly IReadOnlyDictionary<EntityType, string> EntityTypeChipStyles =
        new Dictionary<EntityType, string>
        {
            [EntityType.Creature] = "black on indianred1",
            [EntityType.Building] = "black on steelblue1",
            [EntityType.District] = "black on mediumpurple1",
            [EntityType.Item] = "black on gold1",
            [EntityType.World] = "black on slateblue1",
            [EntityType.Country] = "black on mediumorchid1",
            [EntityType.State] = "black on orchid1",
            [EntityType.City] = "black on plum1",
        };

    extension(AnsiConsole)
    {
        public static void Announce(string message) => AnnounceWithColor("grey", message);

        public static void AnnounceSuccess(string message) => AnnounceWithColor("green", message);

        public static void AnnounceWarning(string message) => AnnounceWithColor("yellow", message);

        public static void AnnounceError(string message) => AnnounceWithColor("red", message);

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

        private static string FormatReputation(int reputation)
        {
            var color = reputation switch
            {
                > 0 => "green",
                < 0 => "red",
                _ => "grey",
            };
            return $"[{color}]{reputation}[/]";
        }

        private static string FormatStatusBars(
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

        private static string FormatBar(int current, int maximum, string color, int width = 20)
        {
            var filled =
                maximum > 0
                    ? Math.Clamp((int)Math.Round(width * (current / (float)maximum)), 0, width)
                    : 0;
            var bar = new string('█', filled) + new string('░', width - filled);
            return $"[{color}]{bar}[/]";
        }

        private static string HealthColor(int currentHp, int maximumHp)
        {
            var percentage = maximumHp > 0 ? currentHp / (float)maximumHp : 0f;
            return percentage switch
            {
                >= 0.66f => "green",
                >= 0.33f => "yellow",
                _ => "red",
            };
        }

        private static string FormatStateChip(Enum value) => FormatChip(value, StateChipColors);

        private static string FormatChip(
            Enum value,
            IReadOnlyDictionary<string, string> colorsByValue
        )
        {
            var color = colorsByValue.GetValueOrDefault(value.ToString(), "grey70");
            return $"[{color} on grey19] {value.ToDisplayName().EscapeMarkup()} [/]";
        }

        public static string FormatNeutralChip(Enum value) =>
            $"[grey70 on grey19] {value.ToDisplayName().EscapeMarkup()} [/]";

        private static string FormatNeutralChip(string value) =>
            $"[grey70 on grey19] {value.EscapeMarkup()} [/]";

        public static void PrintNarration(string text) =>
            AnsiConsole.Markup($"[italic]{text.EscapeMarkup()}[/]");

        public static void PrintEntityChip(string name, EntityType type)
        {
            var style = EntityTypeChipStyles.GetValueOrDefault(type, "grey70 on grey19");
            AnsiConsole.Markup($"[{style}] {name.EscapeMarkup()} [/]");
        }

        private static string FormatDebuffChip(string label, int remainingTurns) =>
            $"[red on grey19] {label.EscapeMarkup()} · {remainingTurns}t [/]";

        private static string FormatBuffChip(string label, int remainingTurns) =>
            $"[blue on grey19] {label.EscapeMarkup()} · {remainingTurns}t [/]";

        private static string FormatHotChip(string label, int remainingTurns) =>
            $"[green on grey19] {label.EscapeMarkup()} · {remainingTurns}t [/]";

        public static void PrintCombatStatus(FightState? combat)
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

        private static Panel BuildCombatantPanel(CombatantState combatant)
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
                    FormatHotChip($"{hot.AbilityName} +{hot.Amount}/turn", hot.RemainingTurns)
                )
            );

            chips.AddRange(
                combatant.ActiveBuffs.Select(buff =>
                {
                    var label = $"{buff.Attribute.ToDisplayName()} {FormatBuffAmount(buff)}";
                    return buff.Amount >= 0
                        ? FormatBuffChip(label, buff.RemainingTurns)
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

        private static string FormatSyntax(Command command)
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

        public static void PrintCreatures(IReadOnlyCollection<CreatureStatusSnapshot> creatures)
        {
            if (creatures.Count == 0)
            {
                AnsiConsole.Announce("Nothing nearby.");
                return;
            }

            AnsiConsole.PrintTable(
                ["Name", "Race", "Level", "Reputation", "Status"],
                creatures.Select(c =>
                    new[]
                    {
                        c.Name,
                        AnsiConsole.FormatNeutralChip(c.CreatureType),
                        c.Level.ToString(),
                        AnsiConsole.FormatReputation(c.Reputation ?? 0),
                        AnsiConsole.FormatStatusBars(
                            c.CurrentHp,
                            c.MaximumHp,
                            c.CurrentAp,
                            c.MaximumAp,
                            c.CurrentMp,
                            c.MaximumMp
                        ),
                    }
                )
            );
        }

        public static void PrintDistricts(
            IReadOnlyCollection<NearbyDistrictSnapshot> districtSnapshots
        )
        {
            if (districtSnapshots.Count == 0)
            {
                AnsiConsole.Announce("No districts nearby.");
                return;
            }

            AnsiConsole.PrintTable(
                ["Name", "Type"],
                districtSnapshots.Select(d =>
                    new[] { d.Name, AnsiConsole.FormatNeutralChip(d.Type) }
                )
            );
        }

        public static void PrintBuildings(IReadOnlyCollection<NearbyBuildingSnapshot> buidings)
        {
            if (buidings.Count == 0)
            {
                AnsiConsole.Announce("No buildings nearby.");
                return;
            }

            AnsiConsole.PrintTable(
                ["Name", "Type"],
                buidings.Select(b => new[] { b.Name, AnsiConsole.FormatNeutralChip(b.Type) })
            );
        }

        public static void PrintDungeons(IReadOnlyCollection<NearbyBuildingSnapshot> dungeons)
        {
            if (dungeons.Count == 0)
            {
                AnsiConsole.Announce("No dungeons nearby.");
                return;
            }

            AnsiConsole.PrintTable(
                ["Name", "Type"],
                dungeons.Select(d => new[] { d.Name, AnsiConsole.FormatNeutralChip(d.Type) })
            );
        }

        public static void PrintProps(IReadOnlyCollection<NearbyPropSnapshot> props)
        {
            if (props.Count == 0)
            {
                AnsiConsole.Announce("No props nearby.");
                return;
            }

            AnsiConsole.PrintTable(
                ["Name", "Type"],
                props.Select(p => new[] { p.Name, AnsiConsole.FormatNeutralChip(p.Type) })
            );
        }

        public static void PrintExits(IReadOnlyCollection<NearbyExitSnapshot> exits)
        {
            if (exits.Count == 0)
            {
                AnsiConsole.Announce("No exits nearby.");
                return;
            }

            AnsiConsole.PrintTable(
                ["Destination", "Description"],
                exits.Select(e => new[] { e.DestinationRoomName, e.Description })
            );
        }

        public static void PrintCreatureDetail(
            IReadOnlyCollection<CreatureStatusSnapshot> creatures,
            string name
        )
        {
            var person = creatures.FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            );
            if (person == null)
            {
                AnsiConsole.AnnounceWarning($"No one named '{name.EscapeMarkup()}' found nearby.");
                return;
            }

            PrintCreatureStatus(person);
        }

        public static void PrintCreatureStatus(CreatureStatusSnapshot status)
        {
            var hpColor = AnsiConsole.HealthColor(status.CurrentHp, status.MaximumHp);
            List<string[]> rows =
            [
                ["Name", status.Name.EscapeMarkup()],
                ["Race", AnsiConsole.FormatNeutralChip(status.CreatureType)],
                ["Gender", AnsiConsole.FormatNeutralChip(status.Gender)],
                ["Level", status.Level.ToString()],
                ["Age", status.Age.ToString()],
                ["Gold", status.Gold.ToString()],
            ];

            if (status.Profession is { } profession)
            {
                rows.Add(["Profession", AnsiConsole.FormatNeutralChip(profession)]);
            }

            if (status.State is { } state)
            {
                rows.Add(["State", AnsiConsole.FormatStateChip(state)]);
            }

            if (status.Reputation is { } reputation)
            {
                rows.Add(["Reputation", AnsiConsole.FormatReputation(reputation)]);
            }

            rows.Add([
                "HP",
                $"{AnsiConsole.FormatBar(status.CurrentHp, status.MaximumHp, hpColor, width: 14)} {status.CurrentHp}/{status.MaximumHp}",
            ]);
            rows.Add([
                "AP",
                $"{AnsiConsole.FormatBar(status.CurrentAp, status.MaximumAp, "blue", width: 14)} {status.CurrentAp}/{status.MaximumAp}",
            ]);
            rows.Add([
                "MP",
                $"{AnsiConsole.FormatBar(status.CurrentMp, status.MaximumMp, "purple", width: 14)} {status.CurrentMp}/{status.MaximumMp}",
            ]);

            if (status.FactionNames is { Count: > 0 } factionNames)
            {
                rows.Add([
                    "Factions",
                    string.Join(" ", factionNames.Select(AnsiConsole.FormatNeutralChip)),
                ]);
            }

            AnsiConsole.PrintTable(["Field", "Value"], rows);
        }

        public static void PrintConnectionStatus(string status)
        {
            AnsiConsole.AnnounceWarning($"[[{status.EscapeMarkup()}]]");
        }

        public static void PrintStatus(SceneSnapshot scene)
        {
            var title =
                $"{AnsiConsole.BuildBreadcrumb(scene).EscapeMarkup()} | {scene.WeekdayName.EscapeMarkup()}, Hour {scene.Hour}";

            AnsiConsole.Write(
                new Padder(
                    new Rule($"[grey]{title}[/]").RuleStyle("grey").LeftJustified(),
                    new Padding(0, 1)
                )
            );
        }

        public static void PrintAvailableCommands(IReadOnlyDictionary<string, Command> commands)
        {
            var rows = commands
                .Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new[] { AnsiConsole.FormatSyntax(c), c.Description ?? "" })
                .Append(["[bold #569CD6]/help[/]", "Show this list"]);

            AnsiConsole.PrintTable(["[bold]Command[/]", "[bold]Description[/]"], rows);
        }
    }
}
