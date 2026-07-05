using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TRPG;
using TRPG.Data;
using TRPG.Endpoints;
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

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

builder.Services
    .AddTrpgLogging(appConfiguration.LogDirectory)
    .AddTrpgDbContext(appConfiguration.PostgresConnectionString)
    .AddOllamaApiClient(appConfiguration)
    .AddSingleton(appConfiguration)
    .AddTrpgApplicationServices();

builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

_ = Task.Run(async () => {
    await using var scope = app.Services.CreateAsyncScope();
    var warmupContext = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
    await warmupContext.Database.CanConnectAsync();
});

app.MapWorldEndpoints();
app.MapSessionEndpoints();

await app.RunAsync();

static string? GetArgValue(string[] args, string flag) {
    var index = Array.IndexOf(args, flag);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
