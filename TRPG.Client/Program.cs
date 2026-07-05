using TRPG.Client;

var serverUrl = GetArgValue(args, "--server") ?? "http://localhost:5000";

using var httpClient = new HttpClient { BaseAddress = new Uri(serverUrl) };
var client = new GameServerClient(httpClient);
var game = new Game(client);
var menu = new Menu(client, game);

await menu.Run(args, CancellationToken.None);

static string? GetArgValue(string[] args, string flag) {
    var index = Array.IndexOf(args, flag);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
