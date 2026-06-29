using Microsoft.EntityFrameworkCore;
using TRPG.Commands;
using TRPG.Data;

namespace TRPG;

internal class Menu(
    TrpgDbContext context,
    BootstrapWorldCommandHandler bootstrapHandler,
    DropWorldCommandHandler dropHandler
) {
    public async Task Run() {
        while (true) {
            Console.WriteLine();
            Console.WriteLine("=== TRPG ===");
            Console.WriteLine("1. New world");
            Console.WriteLine("2. Drop world");
            Console.WriteLine("3. Exit");
            Console.Write("> ");

            switch (Console.ReadLine()?.Trim()) {
                case "1":
                    await RunNew();
                    break;
                case "2":
                    await RunDrop();
                    break;
                case "3":
                    return;
            }
        }
    }

    private async Task RunNew() {
        Console.Write("World description: ");
        var description = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(description)) {
            Console.WriteLine("Description cannot be empty.");
            return;
        }

        Console.WriteLine("Configure generation (press Enter to use defaults):");
        var command = new BootstrapWorldCommand {
            Description = description,
            MinCountries = AskInt("  Countries min", WorldGenerationDefaults.MinCountries),
            MaxCountries = AskInt("  Countries max", WorldGenerationDefaults.MaxCountries),
            MinCities = AskInt("  Cities min", WorldGenerationDefaults.MinCities),
            MaxCities = AskInt("  Cities max", WorldGenerationDefaults.MaxCities),
            HousesPerCity = AskInt("  Houses/city", WorldGenerationDefaults.HousesPerCity),
            RaceCount = AskInt("  Races", WorldGenerationDefaults.RaceCount),
            FactionCount = AskInt("  Factions", WorldGenerationDefaults.FactionCount)
        };

        Console.WriteLine("Generating world...");
        var result = await bootstrapHandler.Handle(command, CancellationToken.None);

        Console.WriteLine($"Done. World \"{result.World.Name}\" created (ID: {result.World.Id}).");
    }

    private static int AskInt(string label, int defaultValue) {
        Console.Write($"{label} [{defaultValue}]: ");
        var input = Console.ReadLine()?.Trim();
        return int.TryParse(input, out var value) ? value : defaultValue;
    }

    private async Task RunDrop() {
        var worlds = await context.Worlds.OrderBy(w => w.Name).ToListAsync();

        if (worlds.Count == 0) {
            Console.WriteLine("No worlds found.");
            return;
        }

        Console.WriteLine();
        for (var i = 0; i < worlds.Count; i++) {
            Console.WriteLine($"{i + 1}. {worlds[i].Name} ({worlds[i].Id})");
        }

        Console.WriteLine("0. Cancel");
        Console.Write("> ");

        var input = Console.ReadLine()?.Trim();
        if (!int.TryParse(input, out var choice) || choice < 1 || choice > worlds.Count) {
            return;
        }

        var world = worlds[choice - 1];
        Console.Write($"Drop \"{world.Name}\"? This cannot be undone. (y/N): ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        await dropHandler.Handle(
            new DropWorldCommand { WorldId = world.Id },
            CancellationToken.None
        );

        Console.WriteLine($"World \"{world.Name}\" dropped.");
    }
}