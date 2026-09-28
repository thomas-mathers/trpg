using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TRPG.Application.Configuration;
using TRPG.Data;

namespace TRPG.Tests.Helpers;

public static class TestWebApplicationFactory
{
    public static WebApplicationFactory<Program> Create(
        string connectionString,
        FakeChatClient chatClient,
        IReadOnlyDictionary<string, string?>? configOverrides = null
    )
    {
        var config = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Trpg"] = connectionString,
            // Guarantees flee always succeeds — hub tests exercise the flee flow's narration and
            // scene-relocation behavior, not the catch-chance mechanic, which has its own
            // dedicated tests at the CombatEngine/ResolveFleeCombatCommand level.
            ["Flee:MinimumCatchChance"] = "0",
            ["Flee:MaximumCatchChance"] = "0",
            // Most tests never register a listening game client (a bare HubConnection, or none at
            // all for HTTP-only endpoint tests), so the production 5s ack wait would be pure dead
            // time on every flush.
            ["GameClientEventAck:AckTimeout"] = "00:00:00.200",
        };
        foreach (var (key, value) in configOverrides ?? new Dictionary<string, string?>())
        {
            config[key] = value;
        }

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(
                (_, configBuilder) => configBuilder.AddInMemoryCollection(config)
            );
            // Skips ZLogger's rolling-file sink and the default console provider — neither is
            // useful for a test run, and both cost real I/O across thousands of log statements.
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<TrpgDbContext>>();
                services.AddDbContext<TrpgDbContext>(options =>
                    options.UseNpgsql(connectionString)
                );

                services.RemoveAll<IChatClient>();
                services.AddKeyedSingleton<IChatClient>(LlmRoleKeys.WorldGeneration, chatClient);
                services.AddKeyedSingleton<IChatClient>(
                    LlmRoleKeys.Gameplay,
                    (_, _) => chatClient.AsBuilder().UseFunctionInvocation().Build()
                );
            });
        });
    }
}
