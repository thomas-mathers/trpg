using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using TRPG;
using TRPG.Extensions;

var defaultConfiguration = new AppConfiguration();
var appConfiguration = new AppConfiguration {
    OllamaModel = GetArgValue(args, "--model") ?? defaultConfiguration.OllamaModel,
    OllamaThink = GetArgValue(args, "--think") is { } thinkArg ? bool.Parse(thinkArg) : defaultConfiguration.OllamaThink,
    OllamaTemperature = GetArgValue(args, "--temperature") is { } temperatureArg
        ? float.Parse(temperatureArg, CultureInfo.InvariantCulture)
        : defaultConfiguration.OllamaTemperature,
    LogDirectory = GetArgValue(args, "--logs") ?? defaultConfiguration.LogDirectory
};

Directory.CreateDirectory(appConfiguration.LogDirectory);

foreach (var old in Directory.GetFiles(appConfiguration.LogDirectory, "trpg_*.log")
             .Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-7))) {
    File.Delete(old);
}

var services = new ServiceCollection()
    .AddTrpgLogging(appConfiguration.LogDirectory)
    .AddTrpgDbContext(appConfiguration.PostgresConnectionString)
    .AddOllamaApiClient(appConfiguration)
    .AddSingleton(appConfiguration)
    .AddMemoryCache()
    .AddTrpgApplicationServices()
    .BuildServiceProvider();

await services.GetRequiredService<Menu>().Run(args, CancellationToken.None);

static string? GetArgValue(string[] args, string flag) {
    var index = Array.IndexOf(args, flag);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}