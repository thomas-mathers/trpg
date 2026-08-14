using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Anthropic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DependencyInjection;
using TickerQ.Utilities.Enums;
using TRPG.Application.Common;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Tools;
using TRPG.Application.Configuration;
using TRPG.Application.Worlds.Commands;
using TRPG.Configuration;
using TRPG.Contracts;
using TRPG.Data;
using TRPG.GameSessions.ChatClients;
using TRPG.GameSessions.Filters;
using TRPG.GameSessions.Hubs;
using TRPG.Worlds.Jobs;
using ZLogger;
using ZLogger.Providers;
using LoggingChatClient = TRPG.GameSessions.ChatClients.LoggingChatClient;

namespace TRPG.Extensions;

internal static class ServiceCollectionExtensions
{
    public const string LocalDevFrontendCorsPolicy = "LocalDevFrontend";

    public static IServiceCollection AddTrpgHostServices(
        this IServiceCollection serviceCollection,
        IConfiguration configuration
    )
    {
        var loggingOptions =
            configuration.GetSection("Logging").Get<LoggingOptions>() ?? new LoggingOptions();

        var signalRBuilder = serviceCollection
            .AddTrpgCors(configuration)
            .AddTrpgLogging(loggingOptions.LogDirectory)
            .AddTrpgDbContext()
            .AddLlmChatClients()
            .AddTrpgOptions(configuration)
            .AddTrpgSessionState()
            .AddTrpgJobs(configuration)
            .AddExceptionHandler<GlobalExceptionHandler>()
            .AddProblemDetails()
            .AddOpenApi()
            .AddResponseCompression(options => options.EnableForHttps = true)
            .AddTrpgJsonOptions()
            .AddSignalR(options => options.AddFilter<HubExceptionTranslationFilter>())
            .AddJsonProtocol(options => ApplyTrpgJsonOptions(options.PayloadSerializerOptions));

        return signalRBuilder.Services;
    }

    private static void ApplyTrpgJsonOptions(JsonSerializerOptions options)
    {
        options.NumberHandling = TrpgJsonOptions.Default.NumberHandling;
        options.PropertyNamingPolicy = TrpgJsonOptions.Default.PropertyNamingPolicy;
        options.PropertyNameCaseInsensitive = TrpgJsonOptions.Default.PropertyNameCaseInsensitive;
        options.DefaultIgnoreCondition = TrpgJsonOptions.Default.DefaultIgnoreCondition;
        foreach (var converter in TrpgJsonOptions.Default.Converters)
        {
            options.Converters.Add(converter);
        }
    }

    public static IServiceCollection AddTrpgCors(
        this IServiceCollection serviceCollection,
        IConfiguration configuration
    )
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        return serviceCollection.AddCors(options =>
        {
            options.AddPolicy(
                LocalDevFrontendCorsPolicy,
                policy =>
                {
                    policy
                        .WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            );
        });
    }

    public static IServiceCollection AddTrpgJsonOptions(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection.ConfigureHttpJsonOptions(options =>
            ApplyTrpgJsonOptions(options.SerializerOptions)
        );

    public static IServiceCollection AddTrpgLogging(
        this IServiceCollection serviceCollection,
        string logDirectory
    )
    {
        Directory.CreateDirectory(logDirectory);

        foreach (
            var old in Directory
                .GetFiles(logDirectory, "trpg_*.log")
                .Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-7))
        )
        {
            File.Delete(old);
        }

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
                    .UseNpgsql(
                        connectionString,
                        sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)
                    )
                    .UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
            }
        );
    }

    public static IServiceCollection AddTrpgSessionState(this IServiceCollection serviceCollection)
    {
        return serviceCollection
            .AddScoped<GameClientEventBuffer>()
            .AddScoped<IGameClientEventSink>(sp => sp.GetRequiredService<GameClientEventBuffer>())
            .AddScoped<IGameClientEventBuffer>(sp => sp.GetRequiredService<GameClientEventBuffer>())
            .AddScoped<GameClientEventDispatcher>()
            .AddScoped<IGameClientEventDispatcher>(sp =>
                sp.GetRequiredService<GameClientEventDispatcher>()
            )
            .AddSingleton<PendingSessionEndRegistry>();
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
                            sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
                        }
                    )
                );
            });
        });
        serviceCollection
            .MapTicker<CreateWorldJob, CreateWorldCommand>()
            .WithPriority(TickerTaskPriority.LongRunning);
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
                        .UseFunctionInvocation(
                            loggerFactory,
                            configure: client =>
                            {
                                client.FunctionInvoker = async (context, cancellationToken) =>
                                {
                                    var stopwatch = Stopwatch.StartNew();
                                    try
                                    {
                                        object? result = await context.Function.InvokeAsync(
                                            context.Arguments,
                                            cancellationToken
                                        );
                                        var outcome = result is ToolError ? "rejected" : "success";
                                        toolLogger.LogInformation(
                                            "[perf] Tool {ToolName}: outcome={Outcome}, resultBytes~={ResultBytes}, total={ElapsedMs}ms",
                                            context.Function.Name,
                                            outcome,
                                            GetSerializedByteCount(result),
                                            stopwatch.ElapsedMilliseconds
                                        );
                                        return result;
                                    }
                                    catch (Exception ex)
                                        when (ex is InvalidOperationException or ArgumentException)
                                    {
                                        toolLogger.LogWarning(
                                            ex,
                                            "[perf] Tool {ToolName}: outcome=rejected, total={ElapsedMs}ms, message={Message}",
                                            context.Function.Name,
                                            stopwatch.ElapsedMilliseconds,
                                            ex.Message
                                        );
                                        return new ToolError(ex.Message);
                                    }
                                    catch (Exception ex) when (ex is not OperationCanceledException)
                                    {
                                        toolLogger.LogError(
                                            ex,
                                            "[perf] Tool {ToolName}: outcome=exception, total={ElapsedMs}ms",
                                            context.Function.Name,
                                            stopwatch.ElapsedMilliseconds
                                        );
                                        throw;
                                    }
                                };
                            }
                        )
                        .Use(innerClient => new LoggingChatClient(
                            innerClient,
                            loggerFactory.CreateLogger<LoggingChatClient>()
                        ))
                        .Use(innerClient => new PromptCachingChatClient(
                            innerClient,
                            loggerFactory.CreateLogger<PromptCachingChatClient>()
                        ))
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
            .Configure<GameClockOptions>(configuration.GetSection("GameClock"))
            .Configure<GameSessionOptions>(configuration.GetSection("GameSession"));
    }

    private static int GetSerializedByteCount(object? value)
    {
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(value).Length;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Encoding.UTF8.GetByteCount(value?.ToString() ?? string.Empty);
        }
    }

    [SuppressMessage(
        "Reliability",
        "CA2000",
        Justification = "Wrapped client is registered as a DI singleton and lives for the app's lifetime"
    )]
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
