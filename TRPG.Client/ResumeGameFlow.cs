using Microsoft.Extensions.Logging;
using Spectre.Console;
using TRPG.Client.Core;
using TRPG.Client.Extensions;
using TRPG.Contracts;

namespace TRPG.Client;

internal sealed class ResumeGameFlow(
    TrpgHttpClient client,
    TrpgHubConnector hubConnector,
    ILogger logger
)
{
    public async Task Run(Guid worldId, CancellationToken cancellationToken)
    {
        var session = await client.StartSession(worldId, cancellationToken);
        await using var gameHub = await hubConnector.Connect(session.SessionId, cancellationToken);

        var narrationRenderer = new NarrationRenderer(
            logger,
            await client.GetNamedEntities(session.SessionId, cancellationToken)
        );

        AnsiConsole.Clear();
        AnsiConsole.Write(
            new Panel(
                "Welcome to the TRPG Game Master!\nType '/exit' to quit, or '/help' for commands."
            )
                .Header("TRPG")
                .BorderColor(Theme.AccentColor)
        );

        gameHub.OnStatusChanged(PrintConnectionStatus);

        if (!await narrationRenderer.TryRender(gameHub.StreamOpening(cancellationToken)))
        {
            return;
        }

        var exitRequested = false;

        while (!exitRequested)
        {
            var fight = await client.GetFight(session.PlayerId, cancellationToken);
            if (fight != null)
            {
                await HandleCombat(narrationRenderer, gameHub, session.PlayerId, cancellationToken);
                continue;
            }

            exitRequested = await HandleExploration(
                narrationRenderer,
                gameHub,
                session.PlayerId,
                session.SessionId,
                cancellationToken
            );
        }
    }

    private async Task<bool> HandleExploration(
        NarrationRenderer narrationRenderer,
        GameHub gameHub,
        Guid playerId,
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        var commandRegistry = new SlashCommandRegistry(
            client,
            narrationRenderer,
            gameHub,
            playerId,
            sessionId
        );

        while (true)
        {
            var fight = await client.GetFight(playerId, cancellationToken);
            if (fight != null)
            {
                return false;
            }

            var scene = await client.GetScene(sessionId, cancellationToken);
            AnsiConsole.PrintStatus(scene);

            AnsiConsole.Write("> ");

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.StartsWith('/'))
            {
                if (await commandRegistry.Handle(input, cancellationToken))
                {
                    return true;
                }

                continue;
            }

            await narrationRenderer.TryRender(gameHub.StreamChat(input, cancellationToken));
        }
    }

    private async Task HandleCombat(
        NarrationRenderer narrationRenderer,
        GameHub gameHub,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var combatMenu = new CombatMenu(client, narrationRenderer, gameHub, playerId);
        var levelBefore = await client.GetLevel(playerId, cancellationToken);

        while (true)
        {
            var fight = await client.GetFight(playerId, cancellationToken);
            if (fight == null)
            {
                break;
            }

            AnsiConsole.PrintCombatStatus(fight);
            await combatMenu.RunTurn(fight, cancellationToken);
        }

        var levelAfter = await client.GetLevel(playerId, cancellationToken);
        if (levelAfter > levelBefore)
        {
            AnsiConsole.AnnounceSuccess(
                $"You leveled up to level {levelAfter}! Run /allocate to spend your attribute points."
            );
        }
    }

    private static void PrintConnectionStatus(ConnectionStatus status) =>
        AnsiConsole.AnnounceWarning($"[[{status.ToDisplayName().EscapeMarkup()}]]");
}
