using TRPG.Contracts;

namespace TRPG.Client;

internal sealed class Menu(GameServerClient client, Game game)
{
    public async Task Run(string[] args, CancellationToken cancellationToken)
    {
        if (args.Contains("--continue"))
        {
            await RunContinue(cancellationToken);
            return;
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== TRPG ===");
            Console.WriteLine("1. New world");
            Console.WriteLine("2. Drop world");
            Console.WriteLine("3. Continue");
            Console.WriteLine("4. Exit");
            Console.Write("> ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1":
                    await RunNew(cancellationToken);
                    break;
                case "2":
                    await RunDrop(cancellationToken);
                    break;
                case "3":
                    await RunContinue(cancellationToken);
                    break;
                default:
                    return;
            }
        }
    }

    private async Task RunNew(CancellationToken cancellationToken)
    {
        var name = PromptForString("Choose your name");
        var gender = PromptForOption(
            "Choose your gender",
            Enum.GetValues<Gender>(),
            r => r.ToString()
        );
        var age = PromptForOption("Choose your age", Enum.GetValues<Age>(), r => r.ToString());
        var race = PromptForOption("Choose your race", Enum.GetValues<Race>(), r => r.ToString());
        var profession = PromptForOption(
            "Choose your profession",
            Enum.GetValues<PlayerClass>(),
            r => r.ToString()
        );

        var request = PromptWorldGenerationParameters(name, gender, age, race, profession);

        Console.WriteLine("Generating world...");

        var world = await client.CreateWorld(request, cancellationToken);

        Console.WriteLine($"\nWorld \"{world.WorldName}\" generated.");
        Console.WriteLine($"Entering \"{world.WorldName}\" as {name} the {profession}...");

        await client.StartSession(world.WorldId, cancellationToken);
        await game.Run(cancellationToken);
    }

    private static CreateWorldRequest PromptWorldGenerationParameters(
        string name,
        Gender gender,
        Age age,
        Race race,
        PlayerClass playerClass
    )
    {
        Console.WriteLine("Configure generation (press Enter to use defaults):");

        return new CreateWorldRequest
        {
            PlayerName = name,
            Race = race,
            Gender = gender,
            Age = age,
            PlayerClass = playerClass,
            Description = PromptForString("  Description", WorldGenerationDefaults.Description),
            MinCityStates = PromptForInt(
                "  City states min",
                WorldGenerationDefaults.MinCityStates
            ),
            MaxCityStates = PromptForInt(
                "  City states max",
                WorldGenerationDefaults.MaxCityStates
            ),
            MinRuralStates = PromptForInt(
                "  Rural states min",
                WorldGenerationDefaults.MinRuralStates
            ),
            MaxRuralStates = PromptForInt(
                "  Rural states max",
                WorldGenerationDefaults.MaxRuralStates
            ),
            MinBuildingsPerState = PromptForInt(
                "  Buildings/state min",
                WorldGenerationDefaults.MinBuildingsPerState
            ),
            MaxBuildingsPerState = PromptForInt(
                "  Buildings/state max",
                WorldGenerationDefaults.MaxBuildingsPerState
            ),
            MinFactionMembers = PromptForInt(
                "  Faction members min",
                WorldGenerationDefaults.MinFactionMembers
            ),
            MaxFactionMembers = PromptForInt(
                "  Faction members max",
                WorldGenerationDefaults.MaxFactionMembers
            ),
            HousesPerCity = PromptForInt("  Houses/city", WorldGenerationDefaults.HousesPerCity),
            MinHouseholdSize = PromptForInt(
                "  Household size min",
                WorldGenerationDefaults.MinHouseholdSize
            ),
            MaxHouseholdSize = PromptForInt(
                "  Household size max",
                WorldGenerationDefaults.MaxHouseholdSize
            ),
            FactionCount = PromptForInt("  Factions", WorldGenerationDefaults.FactionCount),
        };
    }

    private async Task RunContinue(CancellationToken cancellationToken)
    {
        var world = await AutoSelectWorld(cancellationToken);
        if (world == null)
        {
            return;
        }

        await client.StartSession(world.WorldId, cancellationToken);
        await game.Run(cancellationToken);
    }

    private async Task<WorldSummary?> AutoSelectWorld(CancellationToken cancellationToken)
    {
        var worlds = (await client.ListWorlds(cancellationToken))
            .Where(w => w.HasPlayer)
            .OrderBy(w => w.Name)
            .ToArray();

        if (worlds.Length == 0)
        {
            Console.WriteLine("No saved games found.");
            return null;
        }

        return worlds.Length == 1
            ? worlds[0]
            : PromptForOption("Choose a world to continue:", worlds, w => w.Name);
    }

    private async Task RunDrop(CancellationToken cancellationToken)
    {
        var worlds = (await client.ListWorlds(cancellationToken)).OrderBy(w => w.Name).ToArray();

        if (worlds.Length == 0)
        {
            Console.WriteLine("No worlds found.");
            return;
        }

        var world = PromptForOption("Choose a world to drop:", worlds, w => w.Name);

        var confirmed = PromptForConfirmation(
            $"Drop \"{world.Name}\"? This cannot be undone. (y/N): "
        );

        if (!confirmed)
        {
            return;
        }

        await client.DropWorld(world.WorldId, cancellationToken);

        Console.WriteLine($"World \"{world.Name}\" dropped.");
    }

    private static int PromptForInt(string label, int defaultValue)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var input = Console.ReadLine()?.Trim();
        return int.TryParse(input, out var value) ? value : defaultValue;
    }

    private static string PromptForString(string label, string defaultValue)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var value = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static string PromptForString(string label)
    {
        Console.WriteLine(label);

        while (true)
        {
            Console.Write("> ");

            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Invalid input.");
                continue;
            }

            return input;
        }
    }

    private static T PromptForOption<T>(
        string label,
        IReadOnlyList<T> options,
        Func<T, string> labelSelector
    )
    {
        Console.WriteLine(label);
        for (var i = 0; i < options.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {labelSelector(options[i])}");
        }

        while (true)
        {
            Console.Write("> ");

            if (
                !int.TryParse(Console.ReadLine(), out var choiceIndex)
                || choiceIndex < 1
                || choiceIndex > options.Count
            )
            {
                Console.WriteLine("Invalid choice.");
                continue;
            }

            return options[choiceIndex - 1];
        }
    }

    private static bool PromptForConfirmation(string label)
    {
        Console.Write(label);
        return string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
    }
}
