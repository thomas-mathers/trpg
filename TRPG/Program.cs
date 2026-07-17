using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TickerQ.DependencyInjection;
using TRPG;
using TRPG.Application.Common.Extensions;
using TRPG.Configuration;
using TRPG.Contracts;
using TRPG.Data;
using TRPG.Endpoints;
using TRPG.Extensions;
using TRPG.GameSessions.Endpoints;
using TRPG.GameSessions.Filters;
using TRPG.GameSessions.Hubs;
using TRPG.Worlds.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

var loggingOptions =
    builder.Configuration.GetSection("Logging").Get<LoggingOptions>() ?? new LoggingOptions();

Directory.CreateDirectory(loggingOptions.LogDirectory);

foreach (
    var old in Directory
        .GetFiles(loggingOptions.LogDirectory, "trpg_*.log")
        .Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-7))
)
{
    File.Delete(old);
}

builder
    .Services.AddTrpgLogging(loggingOptions.LogDirectory)
    .AddTrpgDbContext()
    .AddLlmChatClients()
    .AddTrpgOptions(builder.Configuration)
    .AddTrpgApplicationServices()
    .AddTrpgSessionState()
    .AddTrpgJobs(builder.Configuration)
    .AddExceptionHandler<GlobalExceptionHandler>()
    .AddProblemDetails()
    .AddSignalR(options => options.AddFilter<GameSessionNotFoundHubFilter>());

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = TrpgJsonOptions.Default.PropertyNamingPolicy;
    options.SerializerOptions.PropertyNameCaseInsensitive = TrpgJsonOptions
        .Default
        .PropertyNameCaseInsensitive;
    options.SerializerOptions.DefaultIgnoreCondition = TrpgJsonOptions
        .Default
        .DefaultIgnoreCondition;
    foreach (var converter in TrpgJsonOptions.Default.Converters)
    {
        options.SerializerOptions.Converters.Add(converter);
    }
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseTickerQ();

_ = Task.Run(async () =>
{
    await using var scope = app.Services.CreateAsyncScope();
    var warmupContext = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
    await warmupContext.Database.CanConnectAsync();
});

app.MapWorldEndpoints();
app.MapGameSessionEndpoints();
app.MapCheatEndpoints();
app.MapJobsEndpoints();
app.MapHub<ChatHub>("/hubs/chat");

await app.RunAsync();
