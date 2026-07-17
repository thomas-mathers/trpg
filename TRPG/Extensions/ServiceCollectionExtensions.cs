using Anthropic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DependencyInjection;
using TRPG.Application.Common;
using TRPG.Application.Common.Tools;
using TRPG.Application.Configuration;
using TRPG.Application.GameSessions;
using TRPG.Application.Worlds.Commands;
using TRPG.Configuration;
using TRPG.Data;
using TRPG.GameSessions.ChatClients;
using TRPG.GameSessions.Hubs;
using TRPG.Worlds.Jobs;
using ZLogger;
using ZLogger.Providers;
using LoggingChatClient = TRPG.GameSessions.ChatClients.LoggingChatClient;

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
                options.IncludeScopes = true;
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter(
                        $"{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}{3}: ",
                        (in template, in info) =>
                        {
                            var scopeText = "";
                            if (info.ScopeState != null)
                            {
                                foreach (var property in info.ScopeState.Properties)
                                {
                                    scopeText += $" {property.Key}={property.Value}";
                                }
                            }

                            template.Format(
                                info.Timestamp.Local,
                                info.LogLevel,
                                info.Category,
                                scopeText
                            );
                        }
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

    public static IServiceCollection AddTrpgSessionState(this IServiceCollection serviceCollection)
    {
        return serviceCollection
            .AddScoped<GameTurnContext>()
            .AddSingleton<WorldConnectionRegistry>();
    }

    public static IServiceCollection AddTrpgJobs(
        this IServiceCollection serviceCollection,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("Trpg");
        serviceCollection.AddTickerQ<TrpgTimeTicker, TrpgCronTicker>(options =>
        {
            options.AddOperationalStore(ef =>
            {
                ef.UseTickerQDbContext<TrpgTickerQDbContext>(db =>
                    db.UseNpgsql(
                        connectionString,
                        sql =>
                        {
                            sql.MigrationsAssembly("TRPG.Data");
                            sql.MigrationsHistoryTable("__TickerQMigrationsHistory");
                        }
                    )
                );
            });
        });
        serviceCollection.MapTicker<CreateWorldJob, CreateWorldCommand>();
        return serviceCollection;
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
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var toolLogger = loggerFactory.CreateLogger(
                        "TRPG.Extensions.GameplayFunctionInvoker"
                    );
                    return CreateChatClient(gameplay.Provider, gameplay.Model, ollamaUri)
                        .AsBuilder()
                        .Use(innerClient => new LoggingChatClient(
                            innerClient,
                            loggerFactory.CreateLogger<LoggingChatClient>()
                        ))
                        .UseFunctionInvocation(
                            loggerFactory,
                            configure: client =>
                            {
                                client.FunctionInvoker = async (context, cancellationToken) =>
                                {
                                    try
                                    {
                                        return await context.Function.InvokeAsync(
                                            context.Arguments,
                                            cancellationToken
                                        );
                                    }
                                    catch (Exception ex)
                                        when (ex is InvalidOperationException or ArgumentException)
                                    {
                                        toolLogger.LogWarning(
                                            ex,
                                            "[tool] {ToolName} failed: {Message}",
                                            context.Function.Name,
                                            ex.Message
                                        );
                                        return new ToolError(ex.Message);
                                    }
                                };
                            }
                        )
                        .Use(innerClient => new PromptCachingChatClient(innerClient))
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
            .Configure<CreatureRegenOptions>(configuration.GetSection("CreatureRegen"))
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
}
