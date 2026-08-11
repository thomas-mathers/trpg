using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TickerQ.DependencyInjection;
using TRPG.Abilities.Endpoints;
using TRPG.Admin.Endpoints;
using TRPG.CreatureGeneration.Endpoints;
using TRPG.Creatures.Endpoints;
using TRPG.Data;
using TRPG.Endpoints;
using TRPG.GameSessions.Endpoints;
using TRPG.GameSessions.Hubs;
using TRPG.Inventory.Endpoints;
using TRPG.Players.Endpoints;
using TRPG.Quests.Endpoints;
using TRPG.Worlds.Endpoints;

namespace TRPG.Extensions;

internal static class WebApplicationExtensions
{
    public static WebApplication UseTrpgServices(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseCors(ServiceCollectionExtensions.LocalDevFrontendCorsPolicy);
        app.UseResponseCompression();
        app.UseTickerQ();

        _ = Task.Run(async () =>
        {
            await using var scope = app.Services.CreateAsyncScope();
            var warmupContext = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            await warmupContext.Database.CanConnectAsync();
        });

        app.Logger.LogInformation(
            "Current environment: {Environment}",
            app.Environment.EnvironmentName
        );

        return app;
    }

    public static WebApplication MapTrpgEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi("/openapi/v1.json")
                .RequireCors(ServiceCollectionExtensions.LocalDevFrontendCorsPolicy);
        }

        app.MapWorldEndpoints();
        app.MapAbilityEndpoints();
        app.MapCreatureEndpoints();
        app.MapPlayerEndpoints();
        app.MapCreatureGenerationEndpoints();
        app.MapGameSessionEndpoints();
        app.MapJobsEndpoints();
        app.MapAdminEndpoints();
        app.MapInventoryEndpoints();
        app.MapQuestEndpoints();
        app.MapHub<ChatHub>("/hubs/chat");

        return app;
    }
}
