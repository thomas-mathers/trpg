using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Serialization;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

public sealed class EndpointTestFixture(PostgreSqlFixture postgres) : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture = new(postgres);
    private WebApplicationFactory<Program>? _factory;

    public FakeChatClient ChatClient { get; } = new();

    internal string ConnectionString => _databaseFixture.ConnectionString;

    public HttpClient CreateClient() => Factory.CreateClient();

    public TestApiClient CreateApiClient() =>
        new(
            Factory.CreateClient(),
            Factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>(),
            Factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.LinkGenerator>(),
            Factory
                .Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
                .Value.SerializerOptions
        );

    public AsyncServiceScope CreateScope() => Factory.Services.CreateAsyncScope();

    public HubConnection CreateHubConnection(Guid sessionId)
    {
        var uri = new Uri(Factory.Server.BaseAddress, $"/hubs/chat?sessionId={sessionId}");
        return new HubConnectionBuilder()
            .WithUrl(
                uri,
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                }
            )
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = TrpgJsonOptions
                    .Default
                    .PropertyNamingPolicy;
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = TrpgJsonOptions
                    .Default
                    .PropertyNameCaseInsensitive;
                options.PayloadSerializerOptions.DefaultIgnoreCondition = TrpgJsonOptions
                    .Default
                    .DefaultIgnoreCondition;
                foreach (var converter in TrpgJsonOptions.Default.Converters)
                {
                    options.PayloadSerializerOptions.Converters.Add(converter);
                }
            })
            .Build();
    }

    private WebApplicationFactory<Program> Factory =>
        _factory
        ?? throw new InvalidOperationException(
            $"{nameof(EndpointTestFixture)} was not initialized."
        );

    public async ValueTask InitializeAsync()
    {
        await _databaseFixture.InitializeAsync();

        // AddTrpgJobs reads ConnectionStrings:Trpg eagerly (a plain captured string, unlike
        // AddTrpgDbContext's per-resolution IConfiguration read), before builder.Build() runs —
        // too early for WebApplicationFactory's ConfigureAppConfiguration override to reach it.
        // The environment variable is picked up by WebApplicationBuilder's own
        // AddEnvironmentVariables() call, which runs synchronously as CreateBuilder(args)'s
        // first configuration source, so it's visible even to that eager read.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Trpg",
            _databaseFixture.ConnectionString
        );

        await using (
            var tickerContext = new TrpgTickerQDbContext(
                new DbContextOptionsBuilder<TrpgTickerQDbContext>()
                    .UseNpgsql(
                        _databaseFixture.ConnectionString,
                        sql =>
                        {
                            sql.MigrationsAssembly("TRPG.Data");
                            sql.MigrationsHistoryTable("__TickerQMigrationsHistory");
                        }
                    )
                    .Options
            )
        )
        {
            await tickerContext.Database.MigrateAsync();
        }

        _factory = TestWebApplicationFactory.Create(_databaseFixture.ConnectionString, ChatClient);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        await _databaseFixture.DisposeAsync();
    }
}

[CollectionDefinition("Endpoints")]
public class EndpointCollection : ICollectionFixture<EndpointTestFixture>;
