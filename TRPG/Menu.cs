using Microsoft.EntityFrameworkCore;
using TRPG.Commands;
using TRPG.Data;
using TRPG.Generators;
using TRPG.Models;

namespace TRPG;

internal class Menu(
    TrpgDbContext context,
    WorldGenerator worldGenerator,
    PersonGenerator personGenerator,
    BootstrapWorldCommandHandler bootstrapHandler,
    DropWorldCommandHandler dropHandler,
    Game game
) {
    public async Task Run(CancellationToken cancellationToken) {
        while (true) {
            Console.WriteLine();
            Console.WriteLine("=== TRPG ===");
            Console.WriteLine("1. New world");
            Console.WriteLine("2. Drop world");
            Console.WriteLine("3. Continue");
            Console.WriteLine("4. Exit");
            Console.Write("> ");

            switch (Console.ReadLine()?.Trim()) {
                case "1":
                    await RunNew(cancellationToken);
                    break;
                case "2":
                    await RunDrop();
                    break;
                case "3":
                    await RunContinue(cancellationToken);
                    break;
                default:
                    return;
            }
        }
    }

    private async Task RunNew(CancellationToken cancellationToken) {
        var playerName = PromptForString("Choose your name");
        var profession = PromptForOption("Choose your profession", Enum.GetValues<Profession>(), r => r.ToString());

        var input = PromptWorldGenerationParameters();

        Console.WriteLine("Generating world...");
        
        var worldResult = await worldGenerator.Generate(input, cancellationToken);

        Console.WriteLine($"\nWorld \"{worldResult.World.Name}\" generated.");

        var selectedRace = PromptForOption("Choose your race", worldResult.Races, r => r.Name);

        var startingRegion = worldResult.Regions.First(r => r.IsCapital);
        
        var playerResult = personGenerator.Generate(
            new PersonGeneratorInput(
                Race: selectedRace,
                Profession: profession,
                WorldId: worldResult.World.Id,
                BirthRegionId: startingRegion.Id,
                Location: new Location {
                    RegionId = startingRegion.Id, 
                    Coordinates = new Point(0, 0)
                },
                Level: 1,
                Name: playerName
            )
        );

        var result = await bootstrapHandler.Handle(worldResult, playerResult, cancellationToken);

        Console.WriteLine($"Entering \"{worldResult.World.Name}\" as {playerName} the {profession}...");
        
        await game.Run(new GameSession(result.WorldId, result.PlayerId), cancellationToken);
    }

    private static WorldGeneratorInput PromptWorldGenerationParameters() {
        Console.WriteLine("Configure generation (press Enter to use defaults):");
        
        var input = new WorldGeneratorInput {
            Description = PromptForString("  Description", WorldGenerationDefaults.Description),
            MinCountries = PromptForInt("  Countries min", WorldGenerationDefaults.MinCountries),
            MaxCountries = PromptForInt("  Countries max", WorldGenerationDefaults.MaxCountries),
            MinUrbanRegions = PromptForInt("  Urban regions min", WorldGenerationDefaults.MinUrbanRegions),
            MaxUrbanRegions = PromptForInt("  Urban regions max", WorldGenerationDefaults.MaxUrbanRegions),
            MinRuralRegions = PromptForInt("  Rural regions min", WorldGenerationDefaults.MinRuralRegions),
            MaxRuralRegions = PromptForInt("  Rural regions max", WorldGenerationDefaults.MaxRuralRegions),
            MinDungeons = PromptForInt("  Dungeons min", WorldGenerationDefaults.MinDungeons),
            MaxDungeons = PromptForInt("  Dungeons max", WorldGenerationDefaults.MaxDungeons),
            MinFactionMembers = PromptForInt("  Faction members min", WorldGenerationDefaults.MinFactionMembers),
            MaxFactionMembers = PromptForInt("  Faction members max", WorldGenerationDefaults.MaxFactionMembers),
            HousesPerCity = PromptForInt("  Houses/city", WorldGenerationDefaults.HousesPerCity),
            RaceCount = PromptForInt("  Races", WorldGenerationDefaults.RaceCount),
            FactionCount = PromptForInt("  Factions", WorldGenerationDefaults.FactionCount)
        };
        
        return input;
    }

    private async Task RunContinue(CancellationToken cancellationToken) {
        var worlds = await context.Worlds
            .Where(w => w.PlayerId != null)
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken);

        if (worlds.Count == 0) {
            Console.WriteLine("No saved games found.");
            return;
        }

        var world = PromptForOption("Choose a world to continue:", worlds, w => w.Name);
        
        await game.Run(new GameSession(world.Id, world.PlayerId!.Value), cancellationToken);
    }
    
    private static int PromptForInt(string label, int defaultValue) {
        Console.Write($"{label} [{defaultValue}]: ");
        var input = Console.ReadLine()?.Trim();
        return int.TryParse(input, out var value) ? value : defaultValue;
    }
    
    private static string PromptForString(string label, string defaultValue) {
        Console.Write($"{label} [{defaultValue}]: ");
        var value = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }
    
    private static string PromptForString(string label) {
        Console.WriteLine(label);
        
        while (true) {
            Console.Write("> ");
            
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrWhiteSpace(input)) {
                Console.WriteLine("Invalid input.");
                continue;
            }

            return input;
        }
    }
    
    private static T PromptForOption<T>(string label, IReadOnlyList<T> options, Func<T, string> labelSelector) {
        Console.WriteLine(label);
        for (var i = 0; i < options.Count; i++) {
            Console.WriteLine($"  {i + 1}. {labelSelector(options[i])}");
        }

        while (true) {
            Console.Write("> ");
            
            if (!int.TryParse(Console.ReadLine(), out var choiceIndex) || choiceIndex < 1 || choiceIndex > options.Count) {
                Console.WriteLine("Invalid choice.");
                continue;
            }
            
            return options[choiceIndex - 1];
        }
    }

    private static bool PromptForConfirmation(string label) {
        Console.Write(label);
        return string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RunDrop() {
        var worlds = await context.Worlds.OrderBy(w => w.Name).ToListAsync(CancellationToken.None);

        if (worlds.Count == 0) {
            Console.WriteLine("No worlds found.");
            return;
        }

        var world = PromptForOption("Choose a world to drop:", worlds, w => w.Name);

        var confirmed = PromptForConfirmation($"Drop \"{world.Name}\"? This cannot be undone. (y/N): ");

        if (!confirmed) {
            return;
        }

        await dropHandler.Handle(
            new DropWorldCommand { WorldId = world.Id },
            CancellationToken.None
        );

        Console.WriteLine($"World \"{world.Name}\" dropped.");
    }
}
