using System.CommandLine;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TRPG.Client.Extensions;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Jobs.Responses;
using TRPG.Contracts.Scenes.Responses;
using TRPG.Contracts.Worlds.Requests;
using TRPG.Contracts.Worlds.Responses;

namespace TRPG.Client;

internal sealed class Game(GameServerClient client, ILogger<Game> logger)
{
    private enum MenuOptions
    {
        New,
        Drop,
        Continue,
        Exit,
    }

    private bool _exitRequested;

    public async Task Start(bool shouldContinue, CancellationToken cancellationToken)
    {
        if (shouldContinue)
        {
            await HandleContinueOption(cancellationToken);
            return;
        }

        AnsiConsole.Write(new FigletText("TRPG").Centered().Color(Color.Gold1));

        while (true)
        {
            var option = await AnsiConsole.PromptAsync(
                new SelectionPrompt<MenuOptions>()
                    .Title("What would you like to do?")
                    .AddChoices(Enum.GetValues<MenuOptions>()),
                cancellationToken
            );

            if (option == MenuOptions.Exit)
            {
                break;
            }

            try
            {
                switch (option)
                {
                    case MenuOptions.New:
                        await HandleNewGameOption(cancellationToken);
                        break;
                    case MenuOptions.Drop:
                        await HandleDropWorldOption(cancellationToken);
                        break;
                    case MenuOptions.Continue:
                        await HandleContinueOption(cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[game] unhandled exception in menu option {Option}", option);
                AnsiConsole.WriteLine();
                AnsiConsole.WriteException(ex);
                AnsiConsole.WriteLine();
            }
        }
    }

    private async Task HandleNewGameOption(CancellationToken cancellationToken)
    {
        var request = PromptForGameOptions();
        if (request == null)
        {
            return;
        }

        var jobId = await client.CreateWorld(request, cancellationToken);

        var jobStatus = await AnsiConsole
            .Status()
            .StartAsync(
                "Generating world...",
                _ => WaitForJobWithProgress(jobId, cancellationToken)
            );

        if (jobStatus.Status != JobStatus.Done)
        {
            AnsiConsole.AnnounceError(
                $"World generation failed: {jobStatus.ErrorMessage?.EscapeMarkup()}"
            );
            return;
        }

        var world = JsonSerializer.Deserialize<CreateWorldResponse>(jobStatus.ResultJson!)!;

        AnsiConsole.AnnounceSuccess($"World \"{world.WorldName.EscapeMarkup()}\" generated.");
        AnsiConsole.WriteLine(
            $"Entering \"{world.WorldName}\" as {request.PlayerName} the {request.PlayerClass}..."
        );

        await ResumeGame(world.WorldId, cancellationToken);
    }

    private static CreateWorldRequest? PromptForGameOptions()
    {
        AnsiConsole.Write(new Rule("Character").RuleStyle("grey").LeftJustified());

        var name = AnsiConsole.Ask<string>("Name");

        var gender = AnsiConsole.Prompt(
            new SelectionPrompt<Gender>().Title("Gender").AddChoices(Enum.GetValues<Gender>())
        );

        AnsiConsole.MarkupLine($"[green]✓[/] Gender: {gender}");

        var age = AnsiConsole.Prompt(
            new SelectionPrompt<Age>().Title("Age").AddChoices(Enum.GetValues<Age>())
        );

        AnsiConsole.MarkupLine($"[green]✓[/] Age: {age}");

        var race = AnsiConsole.Prompt(
            new SelectionPrompt<Race>().Title("Race").AddChoices(Enum.GetValues<Race>())
        );

        AnsiConsole.MarkupLine($"[green]✓[/] Race: {race}");

        var playerClass = AnsiConsole.Prompt(
            new SelectionPrompt<PlayerClass>()
                .Title("Class")
                .AddChoices(Enum.GetValues<PlayerClass>())
        );

        AnsiConsole.MarkupLine($"[green]✓[/] Class: {playerClass}");

        AnsiConsole.Write(new Rule("World").RuleStyle("grey").LeftJustified());

        var description = AnsiConsole.Ask("Description", WorldGenerationDefaults.Description);

        AnsiConsole.Write(new Rule("Geography").RuleStyle("grey").LeftJustified());

        var minCityStates = AnsiConsole.Ask(
            "Min city states",
            WorldGenerationDefaults.MinCityStates
        );

        var maxCityStates = AnsiConsole.Ask(
            "Max city states",
            WorldGenerationDefaults.MaxCityStates
        );

        var minRuralStates = AnsiConsole.Ask(
            "Min rural states",
            WorldGenerationDefaults.MinRuralStates
        );

        var maxRuralStates = AnsiConsole.Ask(
            "Max rural states",
            WorldGenerationDefaults.MaxRuralStates
        );

        AnsiConsole.Write(new Rule("Dungeons").RuleStyle("grey").LeftJustified());

        var minDungeonsPerState = AnsiConsole.Ask(
            "Min dungeons per state",
            WorldGenerationDefaults.MinBuildingsPerState
        );

        var maxDungeonsPerState = AnsiConsole.Ask(
            "Max dungeons per state",
            WorldGenerationDefaults.MaxBuildingsPerState
        );

        AnsiConsole.Write(new Rule("Factions").RuleStyle("grey").LeftJustified());

        var minFactionMembers = AnsiConsole.Ask(
            "Min faction members",
            WorldGenerationDefaults.MinFactionMembers
        );

        var maxFactionMembers = AnsiConsole.Ask(
            "Max faction members",
            WorldGenerationDefaults.MaxFactionMembers
        );

        var numFactions = AnsiConsole.Ask("Num factions", WorldGenerationDefaults.FactionCount);

        AnsiConsole.Write(new Rule("Households").RuleStyle("grey").LeftJustified());

        var housesPerCity = AnsiConsole.Ask("Houses/city", WorldGenerationDefaults.HousesPerCity);

        var minHouseholdSize = AnsiConsole.Ask(
            "Min household size",
            WorldGenerationDefaults.MinHouseholdSize
        );

        var maxHouseholdSize = AnsiConsole.Ask(
            "Max household size",
            WorldGenerationDefaults.MaxHouseholdSize
        );

        AnsiConsole.Write(new Rule("Review").RuleStyle("grey").LeftJustified());

        AnsiConsole.RenderTable(
            ["Setting", "Value"],
            [
                ["Name", name.EscapeMarkup()],
                ["Gender", AnsiConsole.FormatNeutralChip(gender.ToString())],
                ["Age", AnsiConsole.FormatNeutralChip(age.ToString())],
                ["Race", AnsiConsole.FormatNeutralChip(race.ToString())],
                ["Class", AnsiConsole.FormatNeutralChip(playerClass.ToString())],
                ["Description", description.EscapeMarkup()],
                ["City states", $"{minCityStates}–{maxCityStates}"],
                ["Rural states", $"{minRuralStates}–{maxRuralStates}"],
                ["Dungeons/state", $"{minDungeonsPerState}–{maxDungeonsPerState}"],
                ["Faction members", $"{minFactionMembers}–{maxFactionMembers}"],
                ["Factions", numFactions.ToString()],
                ["Houses/city", housesPerCity.ToString()],
                ["Household size", $"{minHouseholdSize}–{maxHouseholdSize}"],
            ]
        );

        if (!AnsiConsole.Confirm("Create this world?"))
        {
            return null;
        }

        return new CreateWorldRequest
        {
            PlayerName = name,
            Race = race,
            Gender = gender,
            Age = age,
            PlayerClass = playerClass,
            Description = description,
            MinCityStates = minCityStates,
            MaxCityStates = maxCityStates,
            MinRuralStates = minRuralStates,
            MaxRuralStates = maxRuralStates,
            MinBuildingsPerState = minDungeonsPerState,
            MaxBuildingsPerState = maxDungeonsPerState,
            MinFactionMembers = minFactionMembers,
            MaxFactionMembers = maxFactionMembers,
            HousesPerCity = housesPerCity,
            MinHouseholdSize = minHouseholdSize,
            MaxHouseholdSize = maxHouseholdSize,
            FactionCount = numFactions,
        };
    }

    private async Task<JobStatusResponse> WaitForJobWithProgress(
        Guid jobId,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            var status = await client.GetJobStatus(jobId, cancellationToken);
            if (status.Status is JobStatus.Done or JobStatus.Failed or JobStatus.Cancelled)
            {
                return status;
            }

            await Task.Delay(2000, cancellationToken);
        }
    }

    private async Task HandleDropWorldOption(CancellationToken cancellationToken)
    {
        var worlds = await client.ListWorlds(cancellationToken);

        if (worlds.Count == 0)
        {
            AnsiConsole.AnnounceWarning("No worlds found.");
            return;
        }

        var world = await AnsiConsole.PromptAsync(
            new SelectionPrompt<WorldSummary>()
                .Title("Choose a world to drop:")
                .UseConverter(world => world.Name)
                .AddChoices(worlds),
            cancellationToken
        );

        var confirmed = await AnsiConsole.ConfirmAsync(
            $"Drop \"{world.Name}\"? This cannot be undone. (y/N): ",
            cancellationToken: cancellationToken
        );

        if (!confirmed)
        {
            return;
        }

        await client.DropWorld(world.WorldId, cancellationToken);

        AnsiConsole.AnnounceSuccess($"World \"{world.Name.EscapeMarkup()}\" dropped.");
    }

    private async Task HandleContinueOption(CancellationToken cancellationToken)
    {
        var world = await AutoSelectWorld(cancellationToken);
        if (world == null)
        {
            return;
        }

        await ResumeGame(world.WorldId, cancellationToken);
    }

    private async Task<WorldSummary?> AutoSelectWorld(CancellationToken cancellationToken)
    {
        var worlds = await client.ListWorlds(cancellationToken);

        if (worlds.Count == 0)
        {
            AnsiConsole.AnnounceWarning("No saved games found.");
            return null;
        }

        return worlds.Count == 1
            ? worlds[0]
            : await AnsiConsole.PromptAsync(
                new SelectionPrompt<WorldSummary>()
                    .Title("Choose a world to continue:")
                    .UseConverter(world => world.Name)
                    .AddChoices(worlds),
                cancellationToken
            );
    }

    private async Task ResumeGame(Guid worldId, CancellationToken cancellationToken)
    {
        _exitRequested = false;
        var session = await client.StartSession(worldId, cancellationToken);
        await using var hubConnection = session.Connection;
        var sessionId = session.SessionId;

        AnsiConsole.Clear();
        AnsiConsole.Write(
            new Panel(
                "Welcome to the TRPG Game Master!\nType '/exit' to quit, or '/help' for commands."
            )
                .Header("TRPG")
                .BorderColor(Color.Gold1)
        );

        hubConnection.Reconnecting += _ =>
        {
            OnConnectionStatusChanged("Connection lost, reconnecting...");
            return Task.CompletedTask;
        };
        hubConnection.Reconnected += _ =>
        {
            OnConnectionStatusChanged("Reconnected.");
            return Task.CompletedTask;
        };
        hubConnection.Closed += _ =>
        {
            OnConnectionStatusChanged("Connection closed.");
            return Task.CompletedTask;
        };

        if (
            !await TryStreamTurn(
                hubConnection.StreamAsync<string>("ReceiveOpening", cancellationToken)
            )
        )
        {
            return;
        }

        await RenderStatus(worldId, sessionId, cancellationToken);

        var commands = BuildCommands(hubConnection, worldId, sessionId);

        while (!_exitRequested)
        {
            AnsiConsole.Write("> ");

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.StartsWith('/'))
            {
                await HandleCommand(input, commands, cancellationToken);
                continue;
            }

            if (
                await TryStreamTurn(
                    hubConnection.StreamAsync<string>("SendChat", input, cancellationToken)
                )
            )
            {
                await RenderStatus(worldId, sessionId, cancellationToken);
            }
        }
    }

    private async Task RenderStatus(
        Guid worldId,
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        var fightState = await TryGetFight(worldId, cancellationToken);
        AnsiConsole.RenderCombatStatus(fightState);

        var scene = await TryGetScene(sessionId, cancellationToken);
        if (scene != null)
        {
            PrintStatus(scene);
        }
    }

    private async Task<SceneSnapshot?> TryGetScene(
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await client.GetScene(sessionId, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "[game] failed to fetch scene");
            AnsiConsole.AnnounceError($"[[Failed to fetch scene: {ex.Message.EscapeMarkup()}]]");
            return null;
        }
    }

    private async Task<FightState?> TryGetFight(Guid worldId, CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetFight(worldId, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "[game] failed to fetch fight status");
            AnsiConsole.AnnounceError(
                $"[[Failed to fetch fight status: {ex.Message.EscapeMarkup()}]]"
            );
            return null;
        }
    }

    private Dictionary<string, Command> BuildCommands(
        HubConnection hubConnection,
        Guid worldId,
        Guid sessionId
    )
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

                await SendWait(hubConnection, worldId, sessionId, hours, cancellationToken);
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
                var scene = await TryGetScene(sessionId, cancellationToken);
                if (scene == null)
                {
                    return 0;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    RenderCreatures(scene);
                }
                else
                {
                    RenderCreatureDetail(scene, name);
                }

                return 0;
            }
        );

        var districtsCommand = new Command("districts", "List districts nearby");
        districtsCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await TryGetScene(sessionId, cancellationToken);
                if (scene != null)
                {
                    RenderDistricts(scene);
                }

                return 0;
            }
        );

        var buildingsCommand = new Command("buildings", "List buildings nearby");
        buildingsCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await TryGetScene(sessionId, cancellationToken);
                if (scene != null)
                {
                    RenderBuildings(scene);
                }

                return 0;
            }
        );

        var dungeonsCommand = new Command("dungeons", "List dungeons nearby");
        dungeonsCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await TryGetScene(sessionId, cancellationToken);
                if (scene != null)
                {
                    RenderDungeons(scene);
                }

                return 0;
            }
        );

        var propsCommand = new Command("props", "List props nearby");
        propsCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await TryGetScene(sessionId, cancellationToken);
                if (scene != null)
                {
                    RenderProps(scene);
                }

                return 0;
            }
        );

        var exitsCommand = new Command("exits", "List exits nearby");
        exitsCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await TryGetScene(sessionId, cancellationToken);
                if (scene != null)
                {
                    RenderExits(scene);
                }

                return 0;
            }
        );

        var characterCommand = new Command("character", "Show your own character sheet");
        characterCommand.SetAction(
            async (_, cancellationToken) =>
            {
                var scene = await TryGetScene(sessionId, cancellationToken);
                if (scene != null)
                {
                    RenderCreatureStatus(scene.PlayerStatus);
                }

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
            exitCommand,
        ];

        return commands.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
    }

    internal async Task SendWait(
        HubConnection hubConnection,
        Guid worldId,
        Guid sessionId,
        int hours,
        CancellationToken cancellationToken
    )
    {
        if (
            await TryStreamTurn(
                hubConnection.StreamAsync<string>("SendWait", hours, cancellationToken)
            )
        )
        {
            await RenderStatus(worldId, sessionId, cancellationToken);
        }
    }

    private void RenderCreatures(SceneSnapshot scene)
    {
        if (scene.NearbyCreatures.Count == 0)
        {
            AnsiConsole.Announce("Nothing nearby.");
            return;
        }

        AnsiConsole.RenderTable(
            ["Name", "Race", "Level", "Reputation", "Status"],
            scene.NearbyCreatures.Select(c =>
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

    private void RenderDistricts(SceneSnapshot scene)
    {
        if (scene.NearbyDistricts.Count == 0)
        {
            AnsiConsole.Announce("No districts nearby.");
            return;
        }

        AnsiConsole.RenderTable(
            ["Name", "Type"],
            scene.NearbyDistricts.Select(d =>
                new[] { d.Name, AnsiConsole.FormatNeutralChip(d.Type) }
            )
        );
    }

    private void RenderBuildings(SceneSnapshot scene)
    {
        if (scene.NearbyBuildings.Count == 0)
        {
            AnsiConsole.Announce("No buildings nearby.");
            return;
        }

        AnsiConsole.RenderTable(
            ["Name", "Type"],
            scene.NearbyBuildings.Select(b =>
                new[] { b.Name, AnsiConsole.FormatNeutralChip(b.Type) }
            )
        );
    }

    private void RenderDungeons(SceneSnapshot scene)
    {
        if (scene.NearbyDungeons.Count == 0)
        {
            AnsiConsole.Announce("No dungeons nearby.");
            return;
        }

        AnsiConsole.RenderTable(
            ["Name", "Type"],
            scene.NearbyDungeons.Select(d =>
                new[] { d.Name, AnsiConsole.FormatNeutralChip(d.Type) }
            )
        );
    }

    private void RenderProps(SceneSnapshot scene)
    {
        if (scene.NearbyProps.Count == 0)
        {
            AnsiConsole.Announce("No props nearby.");
            return;
        }

        AnsiConsole.RenderTable(
            ["Name", "Type"],
            scene.NearbyProps.Select(p => new[] { p.Name, AnsiConsole.FormatNeutralChip(p.Type) })
        );
    }

    private void RenderExits(SceneSnapshot scene)
    {
        if (scene.Exits.Count == 0)
        {
            AnsiConsole.Announce("No exits nearby.");
            return;
        }

        AnsiConsole.RenderTable(
            ["Destination", "Description"],
            scene.Exits.Select(e => new[] { e.DestinationRoomName, e.Description })
        );
    }

    private void RenderCreatureDetail(SceneSnapshot scene, string name)
    {
        var person = scene.NearbyCreatures.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        );
        if (person == null)
        {
            AnsiConsole.AnnounceWarning($"No one named '{name.EscapeMarkup()}' found nearby.");
            return;
        }

        RenderCreatureStatus(person);
    }

    private static void RenderCreatureStatus(CreatureStatusSnapshot status)
    {
        var hpColor = AnsiConsole.HealthColor(status.CurrentHp, status.MaximumHp);
        List<string[]> rows =
        [
            ["Name", status.Name.EscapeMarkup()],
            ["Race", AnsiConsole.FormatNeutralChip(status.CreatureType)],
            ["Gender", AnsiConsole.FormatNeutralChip(status.Gender)],
            ["Profession", AnsiConsole.FormatNeutralChip(status.Profession)],
            ["Level", status.Level.ToString()],
            ["Age", status.Age.ToString()],
            ["Gold", status.Gold.ToString()],
        ];

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

        AnsiConsole.RenderTable(["Field", "Value"], rows);
    }

    private static void OnConnectionStatusChanged(string status)
    {
        AnsiConsole.AnnounceWarning($"[[{status.EscapeMarkup()}]]");
    }

    private async Task<bool> TryStreamTurn(IAsyncEnumerable<string> tokens)
    {
        try
        {
            await foreach (var token in tokens)
            {
                AnsiConsole.Write(token);
            }

            return true;
        }
        catch (Exception ex) when (ex is HubException or IOException)
        {
            logger.LogError(ex, "[game] turn failed while streaming");
            AnsiConsole.AnnounceError($"[[Turn failed: {ex.Message.EscapeMarkup()}]]");
            return false;
        }
    }

    private static void PrintStatus(SceneSnapshot scene)
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

    private static async Task HandleCommand(
        string input,
        IReadOnlyDictionary<string, Command> commands,
        CancellationToken cancellationToken
    )
    {
        var body = input[1..];
        var spaceIndex = body.IndexOf(' ', StringComparison.OrdinalIgnoreCase);
        var name = spaceIndex < 0 ? body : body[..spaceIndex];
        var argumentText = spaceIndex < 0 ? "" : body[(spaceIndex + 1)..].Trim();

        if (name.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            HandleHelpCommand(commands);
            return;
        }

        if (!commands.TryGetValue(name, out var command))
        {
            AnsiConsole.AnnounceWarning(
                $"Unknown command '/{name.EscapeMarkup()}'. Type /help for a list."
            );
            return;
        }

        var parseResult = command.Parse(argumentText);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                AnsiConsole.AnnounceWarning(error.Message.EscapeMarkup());
            }

            return;
        }

        await parseResult.InvokeAsync(cancellationToken: cancellationToken);
    }

    private static void HandleHelpCommand(IReadOnlyDictionary<string, Command> commands)
    {
        var rows = commands
            .Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new[] { AnsiConsole.FormatSyntax(c), c.Description ?? "" })
            .Append(["[bold #569CD6]/help[/]", "Show this list"]);

        AnsiConsole.RenderTable(["[bold]Command[/]", "[bold]Description[/]"], rows);
    }
}
