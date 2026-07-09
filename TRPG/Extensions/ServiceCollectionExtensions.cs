using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using TRPG.Application.Game;
using TRPG.Data;
using ZLogger;
using ZLogger.Providers;

namespace TRPG.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrpgLogging(
        this IServiceCollection serviceCollection,
        string logDirectory
    )
    {
        return serviceCollection.AddLogging(builder =>
        {
            builder.AddZLoggerRollingFile(options =>
            {
                options.FilePathSelector = (timestamp, sequence) =>
                    Path.Combine(
                        logDirectory,
                        $"trpg_{timestamp.LocalDateTime:yyyyMMdd}_{sequence:000}.log"
                    );
                options.RollingInterval = RollingInterval.Day;
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter(
                        $"{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}: ",
                        (in template, in info) =>
                            template.Format(info.Timestamp.Local, info.LogLevel, info.Category)
                    );
                });
            });
        });
    }

    public static IServiceCollection AddTrpgDbContext(
        this IServiceCollection serviceCollection,
        string connectionString
    )
    {
        return serviceCollection.AddDbContext<TrpgDbContext>(
            (provider, options) =>
            {
                options
                    .UseNpgsql(connectionString)
                    .UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
            }
        );
    }

    public static IServiceCollection AddOllamaApiClient(
        this IServiceCollection serviceCollection,
        AppConfiguration appConfiguration
    )
    {
        return serviceCollection.AddSingleton<IOllamaApiClient>(_ =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = appConfiguration.OllamaUri,
                Timeout = Timeout.InfiniteTimeSpan,
            };
            return new OllamaApiClient(httpClient) { SelectedModel = appConfiguration.OllamaModel };
        });
    }

    public static IServiceCollection AddTrpgSessionState(
        this IServiceCollection serviceCollection
    )
    {
        return serviceCollection
            .AddSingleton<GameSessionStore>()
            .AddScoped<CurrentGameSessionAccessor>()
            .AddScoped(sp => sp.GetRequiredService<CurrentGameSessionAccessor>().State.Session)
            .AddScoped(sp => sp.GetRequiredService<CurrentGameSessionAccessor>().State.Chat);
    }
}
