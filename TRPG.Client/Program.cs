using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Client;

var serverOption = new Option<string>("--server")
{
    Description = "Base URL of the TRPG game server",
    DefaultValueFactory = _ => "http://localhost:5000",
};

var continueOption = new Option<bool>("--continue")
{
    Description = "Resume the most recent saved game instead of showing the menu",
};

var rootCommand = new RootCommand("TRPG console client") { serverOption, continueOption };

rootCommand.SetAction(
    async (parseResult, cancellationToken) =>
    {
        var serverUrl = parseResult.GetValue(serverOption)!;
        var shouldContinue = parseResult.GetValue(continueOption);

        using var loggerFactory = ClientLogging.CreateLoggerFactory();

        var services = new ServiceCollection();
        services
            .AddHttpClient(
                "TrpgServer",
                httpClient => httpClient.BaseAddress = new Uri(serverUrl)
            )
            .AddStandardResilienceHandler();
        await using var serviceProvider = services.BuildServiceProvider();
        var httpClient = serviceProvider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("TrpgServer");

        var client = new TrpgHttpClient(httpClient);
        var hubConnector = new TrpgHubConnector(httpClient, loggerFactory);
        var game = new Game(client, hubConnector, loggerFactory.CreateLogger<Game>());

        await game.Start(shouldContinue, cancellationToken);
        return 0;
    }
);

return await rootCommand.Parse(args).InvokeAsync();
