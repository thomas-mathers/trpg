namespace TRPG.Client;

internal sealed class Game(GameServerClient client) {
    public async Task Run(CancellationToken cancellationToken) {
        Console.Clear();
        Console.WriteLine("Welcome to the TRPG Game Master!");
        Console.WriteLine("Type 'exit' to quit.");
        Console.WriteLine();

        await foreach (var token in client.ReceiveOpening(cancellationToken)) {
            Console.Write(token);
        }

        PrintStatus();

        while (true) {
            Console.Write("\n> ");

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) {
                await client.EndSession(cancellationToken);
                break;
            }

            if (input.StartsWith("/wait", StringComparison.OrdinalIgnoreCase)) {
                await HandleWaitCommand(input, cancellationToken);
                continue;
            }

            if (input.Equals("/nearby", StringComparison.OrdinalIgnoreCase)) {
                PrintNearby();
                continue;
            }

            if (input.StartsWith("/inspect", StringComparison.OrdinalIgnoreCase)) {
                HandleInspectCommand(input);
                continue;
            }

            await foreach (var token in client.SendChat(input, cancellationToken)) {
                Console.Write(token);
            }

            PrintStatus();
        }
    }

    private async Task HandleWaitCommand(string input, CancellationToken cancellationToken) {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var hours) || hours <= 0) {
            Console.WriteLine("Usage: /wait <hours>");
            return;
        }

        await foreach (var token in client.SendWait(hours, cancellationToken)) {
            Console.Write(token);
        }

        PrintStatus();
    }

    private void PrintStatus() {
        var scene = client.CurrentScene;
        if (scene == null) {
            return;
        }

        var breadcrumb = string.Join(" > ", new[] { scene.RegionName, scene.CityName, scene.DistrictName, scene.BuildingName, scene.RoomName }
            .Where(name => !string.IsNullOrEmpty(name)));
        Console.WriteLine($"\n[{breadcrumb} | {scene.WeekdayName}, Hour {scene.Hour}]");
    }

    private void PrintNearby() {
        var scene = client.CurrentScene;
        if (scene == null) {
            Console.WriteLine("No scene information available yet.");
            return;
        }

        if (scene.NearbyPeople.Count == 0 && scene.NearbyBuildings.Count == 0 && scene.Exits.Count == 0) {
            Console.WriteLine("Nothing nearby.");
            return;
        }

        if (scene.NearbyPeople.Count > 0) {
            Console.WriteLine("People:");
            foreach (var person in scene.NearbyPeople) {
                Console.WriteLine($"  {person.Name,-20} {person.CreatureType,-10} {person.Profession,-12} Lvl {person.Level,-3} Age {person.Age}");
            }
        }

        if (scene.NearbyBuildings.Count > 0) {
            Console.WriteLine("Buildings:");
            foreach (var building in scene.NearbyBuildings) {
                Console.WriteLine($"  {building.Name,-25} {building.Type}");
            }
        }

        if (scene.Exits.Count > 0) {
            Console.WriteLine("Exits:");
            foreach (var exit in scene.Exits) {
                Console.WriteLine($"  {exit.DestinationRoomName,-25} {exit.Description}");
            }
        }
    }

    private void HandleInspectCommand(string input) {
        var name = input.Length > "/inspect".Length ? input["/inspect".Length..].Trim() : "";
        if (string.IsNullOrWhiteSpace(name)) {
            Console.WriteLine("Usage: /inspect <name>");
            return;
        }

        var scene = client.CurrentScene;
        if (scene == null) {
            Console.WriteLine("No scene information available yet.");
            return;
        }

        var person = scene.NearbyPeople.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (person != null) {
            Console.WriteLine($"{person.Name} — {person.CreatureType} {person.Profession}, Level {person.Level}, Age {person.Age}");
            Console.WriteLine($"State: {person.State}");
            if (person.FactionNames.Count > 0) {
                Console.WriteLine($"Factions: {string.Join(", ", person.FactionNames)}");
            }

            Console.WriteLine($"Reputation: {person.Reputation}");
            return;
        }

        var building = scene.NearbyBuildings.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (building != null) {
            Console.WriteLine($"{building.Name} — {building.Type}");
            return;
        }

        var exit = scene.Exits.FirstOrDefault(e => e.DestinationRoomName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (exit != null) {
            Console.WriteLine($"{exit.DestinationRoomName} — {exit.Description}");
            return;
        }

        Console.WriteLine($"No one or nothing named '{name}' found nearby.");
    }
}
