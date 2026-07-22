using Microsoft.Extensions.Logging;
using Spectre.Console;
using TRPG.Client.Extensions;
using TRPG.Contracts.Worlds.Responses;

namespace TRPG.Client;

internal sealed class Game(
    TrpgHttpClient client,
    TrpgHubConnector hubConnector,
    ILogger<Game> logger
)
{
    private enum MenuOptions
    {
        New,
        Drop,
        Continue,
        Exit,
    }

    private readonly NewGameFlow _newGameFlow = new(client);
    private readonly ResumeGameFlow _resumeGameFlow = new(client, hubConnector, logger);

    public async Task Start(bool shouldContinue, CancellationToken cancellationToken)
    {
        if (shouldContinue)
        {
            await HandleContinueOption(cancellationToken);
            return;
        }

        AnsiConsole.Write(new FigletText("TRPG").Centered().Color(Theme.AccentColor));

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
        var worldId = await _newGameFlow.Run(cancellationToken);
        if (worldId is { } id)
        {
            await _resumeGameFlow.Run(id, cancellationToken);
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

        await _resumeGameFlow.Run(world.WorldId, cancellationToken);
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
}
