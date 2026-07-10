using Anthropic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using TRPG.Application.Common;
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

    public static IServiceCollection AddLlmChatClients(
        this IServiceCollection serviceCollection,
        AppConfiguration appConfiguration
    )
    {
        return serviceCollection
            .AddKeyedSingleton(
                LlmRoleKeys.WorldGeneration,
                (_, _) =>
                    CreateChatClient(appConfiguration.WorldGeneration, appConfiguration.OllamaUri)
            )
            .AddKeyedSingleton(
                LlmRoleKeys.Gameplay,
                (_, _) =>
                    CreateChatClient(appConfiguration.Gameplay, appConfiguration.OllamaUri)
                        .AsBuilder()
                        .UseFunctionInvocation()
                        .Build()
            )
            .AddSingleton(
                new GameplaySettings(
                    appConfiguration.Gameplay.Think,
                    appConfiguration.Gameplay.Temperature
                )
            );
    }

    private static IChatClient CreateChatClient(LlmRoleSettings settings, Uri ollamaUri) =>
        settings.Provider switch
        {
            LlmProvider.Ollama => new OllamaApiClient(
                new HttpClient { BaseAddress = ollamaUri, Timeout = Timeout.InfiniteTimeSpan }
            )
            {
                SelectedModel = settings.Model,
            },
            LlmProvider.Anthropic => new AnthropicClient
            {
                ApiKey = Environment.GetEnvironmentVariable(
                    "ANTHROPIC_API_KEY",
                    EnvironmentVariableTarget.User
                ),
            }.AsIChatClient(settings.Model),
            _ => throw new ArgumentOutOfRangeException(nameof(settings)),
        };

    public static IServiceCollection AddTrpgSessionState(this IServiceCollection serviceCollection)
    {
        return serviceCollection
            .AddSingleton<GameSessionStateStore>()
            .AddScoped<CurrentGameSessionStateAccessor>()
            .AddScoped(sp => sp.GetRequiredService<CurrentGameSessionStateAccessor>().State.Session)
            .AddScoped(sp =>
                sp.GetRequiredService<CurrentGameSessionStateAccessor>().State.Messages
            );
    }
}
