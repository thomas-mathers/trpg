using Microsoft.Extensions.DependencyInjection;
using TRPG;
using TRPG.Extensions;

Directory.CreateDirectory("logs");

foreach (var old in Directory.GetFiles("logs", "trpg_*.log")
             .Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-7))) {
    File.Delete(old);
}

var appConfiguration = new AppConfiguration();

var services = new ServiceCollection()
    .AddTrpgLogging()
    .AddTrpgDbContext(appConfiguration.PostgresConnectionString)
    .AddOllamaApiClient(appConfiguration)
    .AddMemoryCache()
    .AddTrpgApplicationServices()
    .BuildServiceProvider();

await services.GetRequiredService<Menu>().Run(CancellationToken.None);