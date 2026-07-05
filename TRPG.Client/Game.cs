using System.Text.Json;

namespace TRPG.Client;

internal sealed class Game(GameServerClient client) {
    public async Task Run(Guid sessionId, string openingResponse, CancellationToken cancellationToken) {
        Console.Clear();
        Console.WriteLine("Welcome to the TRPG Game Master!");
        Console.WriteLine("Type 'exit' to quit.");
        Console.WriteLine();
        Console.Write(openingResponse);

        while (true) {
            Console.Write("\n> ");

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) {
                await client.EndSession(sessionId, cancellationToken);
                break;
            }

            if (input.StartsWith("/wait", StringComparison.OrdinalIgnoreCase)) {
                await HandleWaitCommand(sessionId, input, cancellationToken);
                continue;
            }

            var response = await client.SendChat(sessionId, input, cancellationToken);
            Console.Write(response.Response);
        }
    }

    private async Task HandleWaitCommand(Guid sessionId, string input, CancellationToken cancellationToken) {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var hours) || hours <= 0) {
            Console.WriteLine("Usage: /wait <hours>");
            return;
        }

        var wait = await client.Wait(sessionId, hours, cancellationToken);
        Console.WriteLine(wait.Message);
        Console.WriteLine(JsonSerializer.Serialize(wait.Scene));
    }
}
