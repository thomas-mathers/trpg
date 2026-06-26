using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using TRPG;
using TRPG.Commands;
using TRPG.Data;
using TRPG.Extensions;
using TRPG.Services;
using ZLogger;
using ZLogger.Providers;

Directory.CreateDirectory("logs");

foreach (var old in Directory.GetFiles("logs", "trpg_*.log")
    .Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-7)))
    File.Delete(old);

var appConfiguration = new AppConfiguration();

var services = new ServiceCollection()
    .AddLogging(builder => {
        builder.SetMinimumLevel(LogLevel.Trace);
        builder.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Error);
        builder.AddZLoggerRollingFile(options => {
            options.FilePathSelector = (timestamp, sequence) =>
                Path.Combine("logs", $"trpg_{timestamp.LocalDateTime:yyyyMMdd}_{sequence:000}.log");
            options.RollingInterval = RollingInterval.Day;
            options.UsePlainTextFormatter(formatter => {
                formatter.SetPrefixFormatter($"{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}: ",
                    (in template, in info) =>
                        template.Format(info.Timestamp.Local, info.LogLevel, info.Category));
            });
        });
    })
    .AddDbContext<TrpgDbContext>((provider, options) => {
        options.UseNpgsql(appConfiguration.PostgresConnectionString)
               .UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
    }, ServiceLifetime.Transient)
    .AddMemoryCache()
    .AddTransient<BuildingService>()
    .AddTransient<FactionService>()
    .AddTransient<InventoryService>()
    .AddTransient<ItemService>()
    .AddTransient<JobService>()
    .AddTransient<LocationService>()
    .AddTransient<NavigationService>()
    .AddTransient<NpcConversationService>()
    .AddTransient<PersonService>()
    .AddTransient<ProfessionService>()
    .AddTransient<QuestService>()
    .AddTransient<RaceService>()
    .AddTransient<ReputationService>()
    .AddTransient<SkillService>()
    .AddTransient<WorldEventService>()
    .AddSingleton<OllamaApiClient>(_ => {
        var httpClient = new HttpClient
            { BaseAddress = appConfiguration.OllamaUri, Timeout = Timeout.InfiniteTimeSpan };
        return new OllamaApiClient(httpClient) { SelectedModel = appConfiguration.OllamaModel };
    })
    .AddSingleton<AiClient>()
    .AddTransient<GenerateGeographyCommandHandler>()
    .AddTransient<GenerateRacesCommandHandler>()
    .AddTransient<GenerateProfessionsCommandHandler>()
    .AddTransient<GenerateFactionsCommandHandler>()
    .AddTransient<GenerateBuildingsCommandHandler>()
    .AddTransient<GenerateSkillsCommandHandler>()
    .AddTransient<GenerateBuildingOwnerCommandHandler>()
    .AddTransient<GenerateWorldCommandHandler>()
    .AddTransient<BootstrapWorldCommandHandler>()
    .AddTransient<DropWorldCommandHandler>()
    .AddTransient<Menu>()
    .BuildServiceProvider();

await services.GetRequiredService<Menu>().Run();
