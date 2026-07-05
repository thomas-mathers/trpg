using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG;
using TRPG.Data;
using TRPG.Endpoints;
using TRPG.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

var appConfiguration = new AppConfiguration();
builder.Configuration.Bind(appConfiguration);

Directory.CreateDirectory(appConfiguration.LogDirectory);

foreach (var old in Directory.GetFiles(appConfiguration.LogDirectory, "trpg_*.log")
             .Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-7))) {
    File.Delete(old);
}

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
