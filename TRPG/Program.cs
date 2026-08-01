using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TickerQ.DependencyInjection;
using TRPG;
using TRPG.Abilities.Endpoints;
using TRPG.Admin.Endpoints;
using TRPG.Application.Common.Extensions;
using TRPG.Configuration;
using TRPG.Contracts;
using TRPG.CreatureGeneration.Endpoints;
using TRPG.Creatures.Endpoints;
using TRPG.Data;
using TRPG.Endpoints;
using TRPG.Extensions;
using TRPG.GameSessions.Endpoints;
using TRPG.GameSessions.Filters;
using TRPG.GameSessions.Hubs;
using TRPG.Inventory.Endpoints;
using TRPG.Players.Endpoints;
using TRPG.Worlds.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins =
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

const string localDevFrontendCorsPolicy = "LocalDevFrontend";

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        localDevFrontendCorsPolicy,
        policy =>
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    );
});

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
    .AddOpenApi()
    .AddResponseCompression(options => options.EnableForHttps = true)
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
app.UseCors(localDevFrontendCorsPolicy);
app.UseResponseCompression();
app.UseTickerQ();

_ = Task.Run(async () =>
{
    await using var scope = app.Services.CreateAsyncScope();
    var warmupContext = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
    await warmupContext.Database.CanConnectAsync();
});

Console.WriteLine($"Current Environment: {app.Environment.EnvironmentName}");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/v1.json").RequireCors(localDevFrontendCorsPolicy);
}

app.MapWorldEndpoints();
app.MapAbilityEndpoints();
app.MapCreatureEndpoints();
app.MapPlayerEndpoints();
app.MapCreatureGenerationEndpoints();
app.MapGameSessionEndpoints();
app.MapJobsEndpoints();
app.MapAdminEndpoints();
app.MapInventoryEndpoints();
app.MapHub<ChatHub>("/hubs/chat");

await app.RunAsync();
