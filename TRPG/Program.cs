using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG;
using TRPG.Application.Common.Extensions;
using TRPG.Data;
using TRPG.Endpoints;
using TRPG.Extensions;
using TRPG.Hubs;

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
    .AddSignalR();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

_ = Task.Run(async () =>
{
    await using var scope = app.Services.CreateAsyncScope();
    var warmupContext = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
    await warmupContext.Database.CanConnectAsync();
});

app.MapWorldEndpoints();
app.MapSessionEndpoints();
app.MapCheatEndpoints();
app.MapHub<ChatHub>("/hubs/chat");

await app.RunAsync();
