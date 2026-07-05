using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Services;
using TRPG.Tools;

namespace TRPG;

internal class Game(
    WorldService worldService,
    SceneService sceneService,
    GameTurnRunner turnRunner,
    ILogger<Game> logger) {
    public async Task Run(GameSession session, CancellationToken cancellationToken) {
        turnRunner.StartSession(session);

        Console.Clear();
        Console.WriteLine("Welcome to the TRPG Game Master!");
        Console.WriteLine("Type 'exit' to quit.");

        await turnRunner.SendOpening(Console.Write, cancellationToken);

        while (true) {
            Console.Write("\n> ");

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) {
                var world = await worldService.GetWorld(session.WorldId, cancellationToken);
                world!.Playtime = GameClock.GetTotalPlaytime(session);
                await worldService.Update(world, cancellationToken);
                break;
            }

            if (input.StartsWith("/wait", StringComparison.OrdinalIgnoreCase)) {
                await HandleWaitCommand(session, input, cancellationToken);
                continue;
            }

            await turnRunner.ProcessTurn(input, Console.Write, cancellationToken);
        }
    }

    private async Task HandleWaitCommand(GameSession session, string input, CancellationToken cancellationToken) {
        logger.LogInformation("[game] >>> {Input}", input);

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var hours) || hours <= 0) {
            Console.WriteLine("Usage: /wait <hours>");
            logger.LogInformation("[game] <<< rejected: usage error");
            return;
        }

        GameClock.AdvanceHours(session, hours);
        var currentDate = GameClock.GetCurrentInGameDate(session);
        Console.WriteLine($"Time passes... it is now {currentDate.WeekdayName}, hour {currentDate.Hour}.");
        logger.LogInformation("[game] <<< waited {Hours}h, now {Weekday} hour {Hour}",
            hours, currentDate.WeekdayName, currentDate.Hour);

        var query = new SceneQuery(session.WorldId, session.PlayerId, currentDate);
        var scene = await sceneService.GetScene(query, cancellationToken);
        var json = JsonSerializer.Serialize(scene, ToolJsonOptions.Options);
        Console.WriteLine(json);
        logger.LogDebug("[wait] scene: {Scene}", json);
    }
}
