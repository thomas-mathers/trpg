using Anthropic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using TRPG.Application.Combat;
using TRPG.Application.Common;
using TRPG.Application.Game;
using TRPG.Application.Worlds.Generators;
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

    public static IServiceCollection AddTrpgDbContext(this IServiceCollection serviceCollection)
    {
        return serviceCollection.AddDbContext<TrpgDbContext>(
            (provider, options) =>
            {
                var connectionString = provider
                    .GetRequiredService<IConfiguration>()
                    .GetConnectionString("Trpg");
                options
                    .UseNpgsql(connectionString)
                    .UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
            }
        );
    }

    public static IServiceCollection AddLlmChatClients(this IServiceCollection serviceCollection)
    {
        return serviceCollection
            .AddKeyedSingleton(
                LlmRoleKeys.WorldGeneration,
                (sp, _) =>
                {
                    var worldGeneration = sp.GetRequiredService<IOptionsMonitor<LlmRoleOptions>>()
                        .Get(LlmRoleKeys.WorldGeneration);
                    var ollamaUri = sp.GetRequiredService<IOptions<OllamaOptions>>().Value.Uri;
                    return CreateChatClient(
                        worldGeneration.Provider,
                        worldGeneration.Model,
                        ollamaUri
                    );
                }
            )
            .AddKeyedSingleton(
                LlmRoleKeys.Gameplay,
                (sp, _) =>
                {
                    var gameplay = sp.GetRequiredService<IOptionsMonitor<LlmRoleOptions>>()
                        .Get(LlmRoleKeys.Gameplay);
                    var ollamaUri = sp.GetRequiredService<IOptions<OllamaOptions>>().Value.Uri;
                    return CreateChatClient(gameplay.Provider, gameplay.Model, ollamaUri)
                        .AsBuilder()
                        .UseFunctionInvocation()
                        .Build();
                }
            );
    }

    public static IServiceCollection AddTrpgOptions(
        this IServiceCollection serviceCollection,
        IConfiguration configuration
    )
    {
        return serviceCollection
            .Configure<OllamaOptions>(configuration.GetSection("Ollama"))
            .Configure<LlmRoleOptions>(
                LlmRoleKeys.WorldGeneration,
                configuration.GetSection("WorldGenerationLlm")
            )
            .Configure<LlmRoleOptions>(
                LlmRoleKeys.Gameplay,
                configuration.GetSection("GameplayLlm")
            )
            .Configure<CombatOptions>(configuration.GetSection("Combat"))
            .Configure<CreatureGeneratorOptions>(configuration.GetSection("CreatureGenerator"))
            .Configure<GameClockOptions>(configuration.GetSection("GameClock"));
    }

    private static IChatClient CreateChatClient(
        LlmProvider provider,
        string model,
        Uri ollamaUri
    ) =>
        provider switch
        {
            LlmProvider.Ollama => new OllamaApiClient(
                new HttpClient { BaseAddress = ollamaUri, Timeout = Timeout.InfiniteTimeSpan }
            )
            {
                SelectedModel = model,
            },
            LlmProvider.Anthropic => new AnthropicClient().AsIChatClient(model),
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

    public static IServiceCollection AddTrpgSessionState(this IServiceCollection serviceCollection)
    {
        return serviceCollection
            .AddSingleton<GameSessionStateStore>()
            .AddScoped<SessionTerminator>()
            .AddScoped<CurrentGameSessionStateAccessor>()
            .AddScoped(sp => sp.GetRequiredService<CurrentGameSessionStateAccessor>().State)
            .AddScoped(sp => sp.GetRequiredService<CurrentGameSessionStateAccessor>().State.Session)
            .AddScoped(sp =>
                sp.GetRequiredService<CurrentGameSessionStateAccessor>().State.Messages
            );
    }
}
