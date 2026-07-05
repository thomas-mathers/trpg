using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;
using TRPG.Commands;
using TRPG.Data;
using TRPG.Definitions;
using TRPG.Generators;
using TRPG.Services;
using TRPG.Tools;
using ZLogger;
using ZLogger.Providers;

namespace TRPG.Extensions;

internal static class ServiceCollectionExtensions {
    public static IServiceCollection AddTrpgLogging(this IServiceCollection serviceCollection, string logDirectory) {
        return serviceCollection.AddLogging(builder => {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddFilter("Microsoft", LogLevel.Error);
            builder.AddFilter("System", LogLevel.Error);
            builder.AddFilter("TRPG", LogLevel.Trace);
            builder.AddZLoggerRollingFile(options => {
                options.FilePathSelector = (timestamp, sequence) =>
                    Path.Combine(logDirectory, $"trpg_{timestamp.LocalDateTime:yyyyMMdd}_{sequence:000}.log");
                options.RollingInterval = RollingInterval.Day;
                options.UsePlainTextFormatter(formatter => {
                    formatter.SetPrefixFormatter($"{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}: ",
                        (in template, in info) =>
                            template.Format(info.Timestamp.Local, info.LogLevel, info.Category));
                });
            });
        });
    }

    public static IServiceCollection AddTrpgDbContext(this IServiceCollection serviceCollection,
        string connectionString) {
        return serviceCollection.AddDbContext<TrpgDbContext>(
            (provider, options) => {
                options.UseNpgsql(connectionString).UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
            }, ServiceLifetime.Scoped);
    }

    public static IServiceCollection AddOllamaApiClient(this IServiceCollection serviceCollection,
        AppConfiguration appConfiguration) {
        return serviceCollection.AddSingleton<IOllamaApiClient>(_ => {
            var httpClient = new HttpClient
                { BaseAddress = appConfiguration.OllamaUri, Timeout = Timeout.InfiniteTimeSpan };
            return new OllamaApiClient(httpClient) { SelectedModel = appConfiguration.OllamaModel };
        });
    }

    public static IServiceCollection AddTrpgApplicationServices(this IServiceCollection serviceCollection) {
        return serviceCollection
            .AddMemoryCache()
            .AddTransient<BuildingService>()
            .AddTransient<InventoryService>()
            .AddTransient<JobService>()
            .AddTransient<SleepJobHandler>()
            .AddTransient<WorkJobHandler>()
            .AddTransient<IdleJobHandler>()
            .AddTransient<JobDispatcher>()
            .AddTransient<JobCatchUpService>()
            .AddTransient<LockService>()
            .AddTransient<LocationService>()
            .AddTransient<NpcConversationService>()
            .AddSingleton(AbilityDefinitions.Create())
            .AddTransient<PersonService>()
            .AddTransient<ReputationService>()
            .AddTransient<SceneService>()
            .AddTransient<WorldService>()
            .AddTransient<WeaponGenerator>()
            .AddTransient<ArmorGenerator>()
            .AddTransient<AccessoryGenerator>()
            .AddTransient<ConsumableGenerator>()
            .AddTransient<AmmoGenerator>()
            .AddTransient<ItemGenerator>()
            .AddTransient<PersonGenerator>()
            .AddTransient<GeographyGenerator>()
            .AddTransient<RaceGenerator>()
            .AddTransient<BuildingGenerator>()
            .AddTransient<FactionsGenerator>()
            .AddTransient<WorldGenerator>()
            .AddTransient<BootstrapWorldCommandHandler>()
            .AddTransient<DropWorldCommandHandler>()
            .AddTransient<GameTurnRunner>()
            .AddSingleton<GameSessionStore>()
            .AddScoped<CurrentGameSessionAccessor>()
            .AddScoped(sp => sp.GetRequiredService<CurrentGameSessionAccessor>().State.Session)
            .AddScoped(sp => sp.GetRequiredService<CurrentGameSessionAccessor>().State.Chat)
            .AddScoped<Tool, WorldInfoTool>()
            .AddScoped<Tool, LookTool>()
            .AddScoped<Tool, MoveTool>()
            .AddScoped<Tool, InventoryTool>()
            .AddScoped<Tool, CharacterTool>()
            .AddScoped<Tool, StartConversationTool>()
            .AddScoped<Tool, EndConversationTool>();
    }
}