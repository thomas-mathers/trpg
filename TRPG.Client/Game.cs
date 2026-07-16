using Microsoft.AspNetCore.SignalR;

namespace TRPG.Client;

internal sealed class Game(GameServerClient client)
{
    public async Task Run(CancellationToken cancellationToken)
    {
        Console.Clear();
        Console.WriteLine("Welcome to the TRPG Game Master!");
        Console.WriteLine("Type 'exit' to quit.");
        Console.WriteLine();

        client.ConnectionStatusChanged += status => Console.WriteLine($"\n[{status}]");

        if (!await TryStreamTurn(client.ReceiveOpening(cancellationToken)))
        {
            return;
        }

        PrintStatus();

        while (true)
        {
            Console.Write("\n> ");

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                await client.EndSession(cancellationToken);
                break;
            }

            if (input.StartsWith("/wait", StringComparison.OrdinalIgnoreCase))
            {
                await HandleWaitCommand(input, cancellationToken);
                continue;
            }

            if (input.Equals("/nearby", StringComparison.OrdinalIgnoreCase))
            {
                PrintNearby();
                continue;
            }

            if (input.StartsWith("/inspect", StringComparison.OrdinalIgnoreCase))
            {
                HandleInspectCommand(input);
                continue;
            }

            if (input.Equals("/cheat", StringComparison.OrdinalIgnoreCase))
            {
                await HandleCheatCommand(cancellationToken);
                continue;
            }

            if (await TryStreamTurn(client.SendChat(input, cancellationToken)))
            {
                PrintStatus();
            }
        }
    }

    private async Task HandleWaitCommand(string input, CancellationToken cancellationToken)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var hours) || hours <= 0)
        {
            Console.WriteLine("Usage: /wait <hours>");
            return;
        }

        if (await TryStreamTurn(client.SendWait(hours, cancellationToken)))
        {
            PrintStatus();
        }
    }

    private async Task HandleCheatCommand(CancellationToken cancellationToken)
    {
        await client.GrantAllAbilities(cancellationToken);
        Console.WriteLine(
            "[Cheat] All abilities granted. AP/MP and cooldowns reset if you're in combat."
        );
    }

    private static async Task<bool> TryStreamTurn(IAsyncEnumerable<string> tokens)
    {
        try
        {
            await foreach (var token in tokens)
            {
                Console.Write(token);
            }

            return true;
        }
        catch (Exception ex) when (ex is HubException or IOException)
        {
            Console.WriteLine($"\n[Turn failed: {ex.Message}]");
            return false;
        }
    }

    private void PrintStatus()
    {
        var scene = client.CurrentScene;
        if (scene == null)
        {
            return;
        }

        var breadcrumb = string.Join(
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
        Console.WriteLine($"\n[{breadcrumb} | {scene.WeekdayName}, Hour {scene.Hour}]");
    }

    private void PrintNearby()
    {
        var scene = client.CurrentScene;
        if (scene == null)
        {
            Console.WriteLine("No scene information available yet.");
            return;
        }

        if (
            scene.NearbyPeople.Count == 0
            && scene.NearbyBuildings.Count == 0
            && scene.NearbyDungeons.Count == 0
            && scene.Exits.Count == 0
            && scene.NearbyDistricts.Count == 0
            && scene.NearbyProps.Count == 0
        )
        {
            Console.WriteLine("Nothing nearby.");
            return;
        }

        if (scene.NearbyPeople.Count > 0)
        {
            Console.WriteLine("People:");
            foreach (var person in scene.NearbyPeople)
            {
                Console.WriteLine(
                    $"  {person.Name, -20} {person.CreatureType, -10} {person.Profession, -12} Lvl {person.Level, -3} Age {person.Age}"
                );
            }
        }

        if (scene.NearbyDistricts.Count > 0)
        {
            Console.WriteLine("Districts:");
            foreach (var district in scene.NearbyDistricts)
            {
                Console.WriteLine($"  {district.Name, -25} {district.Type}");
            }
        }

        if (scene.NearbyBuildings.Count > 0)
        {
            Console.WriteLine("Buildings:");
            foreach (var building in scene.NearbyBuildings)
            {
                Console.WriteLine($"  {building.Name, -25} {building.Type}");
            }
        }

        if (scene.NearbyDungeons.Count > 0)
        {
            Console.WriteLine("Dungeons:");
            foreach (var dungeon in scene.NearbyDungeons)
            {
                Console.WriteLine($"  {dungeon.Name, -25} {dungeon.Type}");
            }
        }

        if (scene.NearbyProps.Count > 0)
        {
            Console.WriteLine("Props:");
            foreach (var prop in scene.NearbyProps)
            {
                Console.WriteLine($"  {prop.Name, -25} {prop.Type}");
            }
        }

        if (scene.Exits.Count > 0)
        {
            Console.WriteLine("Exits:");
            foreach (var exit in scene.Exits)
            {
                Console.WriteLine($"  {exit.DestinationRoomName, -25} {exit.Description}");
            }
        }
    }

    private void HandleInspectCommand(string input)
    {
        var name = input.Length > "/inspect".Length ? input["/inspect".Length..].Trim() : "";
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.WriteLine("Usage: /inspect <name>");
            return;
        }

        var scene = client.CurrentScene;
        if (scene == null)
        {
            Console.WriteLine("No scene information available yet.");
            return;
        }

        var person = scene.NearbyPeople.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        );
        if (person != null)
        {
            Console.WriteLine(
                $"{person.Name} — {person.CreatureType} {person.Profession}, Level {person.Level}, Age {person.Age}"
            );
            Console.WriteLine($"State: {person.State}");
            if (person.FactionNames.Count > 0)
            {
                Console.WriteLine($"Factions: {string.Join(", ", person.FactionNames)}");
            }

            Console.WriteLine($"Reputation: {person.Reputation}");
            return;
        }

        var building = scene.NearbyBuildings.FirstOrDefault(b =>
            b.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        );
        if (building != null)
        {
            Console.WriteLine($"{building.Name} — {building.Type}");
            return;
        }

        var dungeon = scene.NearbyDungeons.FirstOrDefault(d =>
            d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        );
        if (dungeon != null)
        {
            Console.WriteLine($"{dungeon.Name} — {dungeon.Type}");
            return;
        }

        var exit = scene.Exits.FirstOrDefault(e =>
            e.DestinationRoomName.Equals(name, StringComparison.OrdinalIgnoreCase)
        );
        if (exit != null)
        {
            Console.WriteLine($"{exit.DestinationRoomName} — {exit.Description}");
            return;
        }

        Console.WriteLine($"No one or nothing named '{name}' found nearby.");
    }
}
