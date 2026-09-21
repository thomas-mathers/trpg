using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Application.Common.Commands;
using TRPG.Application.LocationSimulation.Commands;

namespace TRPG.Quests.Endpoints;

// Manual-testing endpoint: exercises the real SeedLlmQuestChainCommand -> TickerQ job pipeline
// end to end against a real world/location, exactly as SyncQuestSeedScheduleCommand would trigger
// it in play. Not used by the SPA. Generation takes 50-70 seconds and runs in the background — the
// response only reports whether a generation request was scheduled, not the result. Once the job
// finishes, GenerateQuestChainCommandHandler logs the full generated DAG at Info level (search the
// server log for "Generated quest chain").
internal static class QuestChainDevEndpoints
{
    public static void MapQuestChainDevEndpoints(this WebApplication app)
    {
        app.MapPost("/dev/quest-chains/generate", GenerateChain).WithName("DevGenerateQuestChain");
    }

    private static async Task<Ok<bool>> GenerateChain(
        Guid worldId,
        Guid locationId,
        Guid playerId,
        int playerLevel,
        [FromServices] ICommandHandler<SeedLlmQuestChainCommand, bool> seedLlmQuestChain,
        CancellationToken cancellationToken,
        Guid? giverFactionId = null
    )
    {
        var scheduled = await seedLlmQuestChain.Handle(
            new SeedLlmQuestChainCommand
            {
                WorldId = worldId,
                PlayerId = playerId,
                LocationId = locationId,
                PlayerLevel = playerLevel,
                GiverFactionId = giverFactionId,
            },
            cancellationToken
        );

        return TypedResults.Ok(scheduled);
    }
}
