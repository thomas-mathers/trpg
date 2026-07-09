using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OllamaSharp;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

public sealed class EndpointTestFixture : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture = new();
    private WebApplicationFactory<Program>? _factory;

    public FakeOllamaApiClient OllamaClient { get; } = new();

    public HttpClient CreateClient() => Factory.CreateClient();

    public AsyncServiceScope CreateScope() => Factory.Services.CreateAsyncScope();

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

                services.RemoveAll<IOllamaApiClient>();
                services.AddSingleton<IOllamaApiClient>(OllamaClient);
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
