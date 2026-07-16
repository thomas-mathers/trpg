using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TRPG.Application.Common;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

public sealed class EndpointTestFixture : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture = new();
    private WebApplicationFactory<Program>? _factory;

    public FakeChatClient ChatClient { get; } = new();

    public HttpClient CreateClient() => Factory.CreateClient();

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

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<TrpgDbContext>>();
                services.AddDbContext<TrpgDbContext>(options =>
                    options.UseNpgsql(_databaseFixture.ConnectionString)
                );

                services.RemoveAll<IChatClient>();
                services.AddKeyedSingleton<IChatClient>(LlmRoleKeys.WorldGeneration, ChatClient);
                services.AddKeyedSingleton<IChatClient>(
                    LlmRoleKeys.Gameplay,
                    (_, _) => ((IChatClient)ChatClient).AsBuilder().UseFunctionInvocation().Build()
                );
            });
        });
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
