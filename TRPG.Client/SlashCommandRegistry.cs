using System.CommandLine;
using System.Globalization;
using Spectre.Console;
using TRPG.Client.Core;
using TRPG.Client.Extensions;
using TRPG.Contracts;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Creatures.Responses;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Client;

internal sealed class SlashCommandRegistry(
    TrpgHttpClient client,
    NarrationRenderer narrationRenderer,
    GameHub gameHub,
    Guid playerId,
    Guid sessionId
)
{
    private bool _exitRequested;

    private IReadOnlyDictionary<string, Command> Commands => field ??= BuildCommands();

    public async Task<bool> Handle(string input, CancellationToken cancellationToken)
    {
        var body = input[1..];
        var spaceIndex = body.IndexOf(' ', StringComparison.OrdinalIgnoreCase);
        var name = spaceIndex < 0 ? body : body[..spaceIndex];
        var argumentText = spaceIndex < 0 ? "" : body[(spaceIndex + 1)..].Trim();

        if (name.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            PrintAvailableCommands(Commands);
            return false;
        }

        if (!Commands.TryGetValue(name, out var command))
        {
            AnsiConsole.AnnounceWarning(
                $"Unknown command '/{name.EscapeMarkup()}'. Type /help for a list."
            );
            return false;
        }

        var parseResult = command.Parse(argumentText);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                AnsiConsole.AnnounceWarning(error.Message.EscapeMarkup());
            }

            return false;
        }

        await parseResult.InvokeAsync(cancellationToken: cancellationToken);
        return _exitRequested;
    }

    private Dictionary<string, Command> BuildCommands()
    {
        var hoursArgument = new Argument<int>("hours") { Description = "Number of hours to wait" };
        var waitCommand = new Command("wait", "Pass time without acting") { hoursArgument };
        waitCommand.SetAction(
            async (parseResult, cancellationToken) =>
            {
                var hours = parseResult.GetValue(hoursArgument);
                if (hours <= 0)
                {
                    AnsiConsole.AnnounceWarning("Usage: /wait <hours>");
                    return 0;
                }

                await narrationRenderer.TryRender(gameHub.StreamWait(hours, cancellationToken));

                return 0;
            }
        );

        var creatureNameArgument = new Argument<string[]>("name")
        {
            Description = "Name of a specific person to inspect (omit to list everyone nearby)",
            Arity = ArgumentArity.ZeroOrMore,
        };
        var creaturesCommand = new Command(
            "creatures",
            "List people nearby, or inspect one by name"
        )
        {
            creatureNameArgument,
        };
        creaturesCommand.SetAction(
            async (parseResult, cancellationToken) =>
            {
                var name = string.Join(' ', parseResult.GetValue(creatureNameArgument) ?? []);
                var scene = await client.GetScene(sessionId, cancellationToken);

                if (string.IsNullOrWhiteSpace(name))
                {
                    PrintCreatures(scene.NearbyCreatures);
                }
                else
                {
                    PrintCreatureDetail(scene.NearbyCreatures, name);
                }

                return 0;
            }
        );

        var districtsCommand = new Command("districts", "List districts nearby");
        districtsCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await client.GetScene(sessionId, cancellationToken);
                PrintDistricts(scene.NearbyDistricts);

                return 0;
            }
        );

        var buildingsCommand = new Command("buildings", "List buildings nearby");
        buildingsCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await client.GetScene(sessionId, cancellationToken);
                PrintBuildings(scene.NearbyBuildings);

                return 0;
            }
        );

        var dungeonsCommand = new Command("dungeons", "List dungeons nearby");
        dungeonsCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await client.GetScene(sessionId, cancellationToken);
                PrintDungeons(scene.NearbyDungeons);

                return 0;
            }
        );

        var propsCommand = new Command("props", "List props nearby");
        propsCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await client.GetScene(sessionId, cancellationToken);
                PrintProps(scene.NearbyProps);

                return 0;
            }
        );

        var exitsCommand = new Command("exits", "List exits nearby");
        exitsCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await client.GetScene(sessionId, cancellationToken);
                PrintExits(scene.Exits);

                return 0;
            }
        );

        var characterCommand = new Command("character", "Show your own character sheet");
        characterCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await client.GetScene(sessionId, cancellationToken);
                PrintCreatureStatus(scene.PlayerStatus);

                return 0;
            }
        );

        var abilityCommand = new Command("ability", "List your abilities");
        abilityCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var abilities = await client.GetAbilities(playerId, cancellationToken);
                PrintAbilities(abilities);
                return 0;
            }
        );

        var skillsCommand = new Command("skills", "Show your skill levels and progress");
        skillsCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var skills = await client.GetSkills(playerId, cancellationToken);
                PrintSkills(skills);
                return 0;
            }
        );

        var allocateCommand = new Command(
            "allocate",
            "Spend any attribute points you've earned from leveling up"
        );
        allocateCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var menu = new AttributeAllocationMenu(client, playerId);
                await menu.Run(cancellationToken);
                return 0;
            }
        );

        var exitCommand = new Command("exit", "End the session and return to the menu");
        exitCommand.SetAction(_ =>
        {
            _exitRequested = true;
            return 0;
        });

        Command[] commands =
        [
            waitCommand,
            creaturesCommand,
            districtsCommand,
            buildingsCommand,
            dungeonsCommand,
            propsCommand,
            exitsCommand,
            characterCommand,
            abilityCommand,
            skillsCommand,
            allocateCommand,
            exitCommand,
        ];

        return commands.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static void Announce(string message) =>
        AnsiConsole.Write(
            new Padder(new Markup($"[{Theme.Neutral}]{message}[/]"), new Padding(0, 1))
        );

    private static void PrintCreatures(IReadOnlyCollection<CreatureStatusSnapshot> creatures)
    {
        if (creatures.Count == 0)
        {
            Announce("Nothing nearby.");
            return;
        }

        AnsiConsole.PrintTable(
            ["Name", "Race", "Level", "Reputation", "Status"],
            creatures.Select(c =>
                new[]
                {
                    c.Name,
                    AnsiConsole.FormatCreatureChip(c.CreatureType),
                    c.Level.ToString(CultureInfo.InvariantCulture),
                    FormatReputation(c.Reputation ?? 0),
                    FormatStatusBars(
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

    private static void PrintDistricts(
        IReadOnlyCollection<NearbyDistrictSnapshot> districtSnapshots
    )
    {
        if (districtSnapshots.Count == 0)
        {
            Announce("No districts nearby.");
            return;
        }

        AnsiConsole.PrintTable(
            ["Name", "Type"],
            districtSnapshots.Select(d => new[] { d.Name, AnsiConsole.FormatDistrictChip(d.Type) })
        );
    }

    private static void PrintBuildings(IReadOnlyCollection<NearbyBuildingSnapshot> buidings)
    {
        if (buidings.Count == 0)
        {
            Announce("No buildings nearby.");
            return;
        }

        AnsiConsole.PrintTable(
            ["Name", "Type"],
            buidings.Select(b => new[] { b.Name, AnsiConsole.FormatBuildingChip(b.Type) })
        );
    }

    private static void PrintDungeons(IReadOnlyCollection<NearbyBuildingSnapshot> dungeons)
    {
        if (dungeons.Count == 0)
        {
            Announce("No dungeons nearby.");
            return;
        }

        AnsiConsole.PrintTable(
            ["Name", "Type"],
            dungeons.Select(d => new[] { d.Name, AnsiConsole.FormatBuildingChip(d.Type) })
        );
    }

    private static void PrintProps(IReadOnlyCollection<NearbyPropSnapshot> props)
    {
        if (props.Count == 0)
        {
            Announce("No props nearby.");
            return;
        }

        AnsiConsole.PrintTable(
            ["Name", "Type"],
            props.Select(p => new[] { p.Name, FormatNeutralChip(p.Type) })
        );
    }

    private static void PrintExits(IReadOnlyCollection<NearbyExitSnapshot> exits)
    {
        if (exits.Count == 0)
        {
            Announce("No exits nearby.");
            return;
        }

        AnsiConsole.PrintTable(
            ["Destination", "Description"],
            exits.Select(e => new[] { e.DestinationRoomName, e.Description })
        );
    }

    private static void PrintCreatureDetail(
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

    private static void PrintCreatureStatus(CreatureStatusSnapshot status)
    {
        var hpColor = AnsiConsole.HealthColor(status.CurrentHp, status.MaximumHp);
        List<string[]> rows =
        [
            ["Name", status.Name.EscapeMarkup()],
            ["Race", AnsiConsole.FormatCreatureChip(status.CreatureType)],
            ["Gender", AnsiConsole.FormatNeutralChip(status.Gender)],
            ["Level", status.Level.ToString(CultureInfo.InvariantCulture)],
            ["Age", status.Age.ToString(CultureInfo.InvariantCulture)],
            ["Gold", status.Gold.ToString(CultureInfo.InvariantCulture)],
        ];

        if (status.Profession is { } profession)
        {
            rows.Add(["Profession", AnsiConsole.FormatNeutralChip(profession)]);
        }

        if (status.State is { } state)
        {
            rows.Add(["State", FormatStateChip(state)]);
        }

        if (status.Reputation is { } reputation)
        {
            rows.Add(["Reputation", FormatReputation(reputation)]);
        }

        rows.Add([
            "HP",
            $"{AnsiConsole.FormatBar(status.CurrentHp, status.MaximumHp, hpColor, width: 14)} {status.CurrentHp}/{status.MaximumHp}",
        ]);
        rows.Add([
            "AP",
            $"{AnsiConsole.FormatBar(status.CurrentAp, status.MaximumAp, Theme.ApBar, width: 14)} {status.CurrentAp}/{status.MaximumAp}",
        ]);
        rows.Add([
            "MP",
            $"{AnsiConsole.FormatBar(status.CurrentMp, status.MaximumMp, Theme.MpBar, width: 14)} {status.CurrentMp}/{status.MaximumMp}",
        ]);
        rows.Add([
            "XP",
            $"{AnsiConsole.FormatBar(status.ExperienceCurrent, status.ExperienceToNextLevel, Theme.XpBar, width: 14)} {status.ExperienceCurrent}/{status.ExperienceToNextLevel}",
        ]);

        if (status.FactionNames is { Count: > 0 } factionNames)
        {
            rows.Add(["Factions", string.Join(" ", factionNames.Select(FormatNeutralChip))]);
        }

        AnsiConsole.PrintTable(["Field", "Value"], rows);
    }

    private static void PrintSkills(IReadOnlyCollection<SkillProgressSummary> skills)
    {
        if (skills.Count == 0)
        {
            Announce("No skills learned.");
            return;
        }

        AnsiConsole.PrintTable(
            ["Skill", "Level", "Experience"],
            skills.Select(s =>
                new[]
                {
                    AnsiConsole.FormatNeutralChip(s.Skill),
                    s.Level.ToString(CultureInfo.InvariantCulture),
                    $"{AnsiConsole.FormatBar(s.ExperienceCurrent, s.ExperienceToNextLevel, Theme.XpBar, width: 14)} {s.ExperienceCurrent}/{s.ExperienceToNextLevel}",
                }
            )
        );
    }

    private static void PrintAbilities(IReadOnlyCollection<AbilitySummary> abilities)
    {
        if (abilities.Count == 0)
        {
            Announce("No abilities known.");
            return;
        }

        AnsiConsole.PrintTable(
            ["Name", "Tree", "Description", "AP", "MP", "Cooldown"],
            abilities.Select(a =>
                new[]
                {
                    a.Name,
                    AnsiConsole.FormatNeutralChip(a.Skill),
                    a.Description,
                    a.ApCost.ToString(CultureInfo.InvariantCulture),
                    a.MpCost.ToString(CultureInfo.InvariantCulture),
                    a.Cooldown.ToString(CultureInfo.InvariantCulture),
                }
            )
        );
    }

    private static void PrintAvailableCommands(IReadOnlyDictionary<string, Command> commands)
    {
        var rows = commands
            .Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new[] { FormatSyntax(c), c.Description ?? "" })
            .Append([$"[bold {Theme.CommandSyntax}]/help[/]", "Show this list"]);

        AnsiConsole.PrintTable(["[bold]Command[/]", "[bold]Description[/]"], rows);
    }

    private static string FormatReputation(int reputation)
    {
        var color = reputation switch
        {
            > 0 => Theme.Positive,
            < 0 => Theme.Negative,
            _ => Theme.Neutral,
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
        var hpColor = AnsiConsole.HealthColor(currentHp, maximumHp);
        return string.Join(
            "\n",
            $"HP {AnsiConsole.FormatBar(currentHp, maximumHp, hpColor, width: 8)} {currentHp}/{maximumHp}",
            $"AP {AnsiConsole.FormatBar(currentAp, maximumAp, Theme.ApBar, width: 8)} {currentAp}/{maximumAp}",
            $"MP {AnsiConsole.FormatBar(currentMp, maximumMp, Theme.MpBar, width: 8)} {currentMp}/{maximumMp}"
        );
    }

    private static string FormatStateChip(CreatureState value) =>
        $"[{Theme.ChipForeground} on {Theme.CreatureStateAccent}] {value.ToDisplayName().EscapeMarkup()} [/]";

    private static string FormatNeutralChip(string value) =>
        $"[{Theme.ChipForeground} on {Theme.NeutralAccent}] {value.EscapeMarkup()} [/]";

    private static string FormatSyntax(Command command)
    {
        var argumentsText = string.Concat(
            command.Arguments.Select(a => $" [{Theme.Neutral} italic]<{a.Name}>[/]")
        );
        return $"[bold {Theme.CommandSyntax}]/{command.Name}[/]{argumentsText}";
    }
}
