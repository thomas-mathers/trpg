using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TickerQ.Utilities;
using TickerQ.Utilities.Interfaces.Managers;
using TRPG.Application.Common.Handling;
using TRPG.Application.Narration.Commands;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Contracts.Worlds.Requests;
using TRPG.Contracts.Worlds.Responses;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Jobs.Responses;
using TRPG.Worlds.Jobs;

namespace TRPG.Worlds.Endpoints;

internal static class WorldEndpoints
{
    public static void MapWorldEndpoints(this WebApplication app)
    {
        app.MapPost("/worlds", CreateWorld).WithName("CreateWorld");
        app.MapGet("/worlds", ListWorlds).WithName("ListWorlds");
        app.MapDelete("/worlds/{worldId:guid}", DropWorld).WithName("DropWorld");
    }

    private static async Task<Accepted<EnqueueJobResponse>> CreateWorld(
        CreateWorldRequest request,
        ITimeTickerManager<TrpgTimeTicker> timeTicker,
        CancellationToken cancellationToken
    )
    {
        var command = new CreateWorldCommand
        {
            WorldInput = new WorldGeneratorInput
            {
                Description = request.Description,
                MinCityStates = request.MinCityStates,
                MaxCityStates = request.MaxCityStates,
                MinRuralStates = request.MinRuralStates,
                MaxRuralStates = request.MaxRuralStates,
                MinBuildingsPerState = request.MinBuildingsPerState,
                MaxBuildingsPerState = request.MaxBuildingsPerState,
                MinFactionMembers = request.MinFactionMembers,
                MaxFactionMembers = request.MaxFactionMembers,
                HousesPerCity = request.HousesPerCity,
                MinHouseholdSize = request.MinHouseholdSize,
                MaxHouseholdSize = request.MaxHouseholdSize,
                FactionCount = request.FactionCount,
            },
            Race = request.Race,
            PlayerClass = request.PlayerClass,
            Name = request.PlayerName,
            Age = request.Age,
            Gender = request.Gender,
            StartingAttributeAllocation = request.StartingAttributeAllocation.ToDictionary(),
        };

        var result = await timeTicker.AddAsync(
            new TrpgTimeTicker
            {
                Request = TickerHelper.CreateTickerRequest(command),
                ExecutionTime = DateTime.UtcNow,
                Function = TickerFunctionProvider.GetFunctionName<CreateWorldJob>(),
                Retries = 3,
                RetryIntervals = [1, 2, 4],
            },
            cancellationToken
        );

        var jobId = result.Result.Id;
        return TypedResults.Accepted($"/jobs/{jobId}", new EnqueueJobResponse(jobId));
    }

    private static async Task<Ok<WorldSummary[]>> ListWorlds(
        [FromServices] IQueryHandler<GetAllWorldsQuery, IReadOnlyList<World>> getAllWorlds,
        CancellationToken cancellationToken
    )
    {
        var worlds = await getAllWorlds.Handle(new GetAllWorldsQuery(), cancellationToken);
        return TypedResults.Ok(
            worlds.Select(w => new WorldSummary(w.Id, w.Name, w.PlayerId != null)).ToArray()
        );
    }

    private static async Task<NoContent> DropWorld(
        Guid worldId,
        [FromServices] ICommandHandler<DropWorldCommand> dropHandler,
        [FromServices]
            ICommandHandler<InvalidateWorldLoreAnchorsCommand> invalidateWorldLoreAnchors,
        CancellationToken cancellationToken
    )
    {
        await dropHandler.Handle(new DropWorldCommand { WorldId = worldId }, cancellationToken);

        await invalidateWorldLoreAnchors.Handle(
            new InvalidateWorldLoreAnchorsCommand { WorldId = worldId },
            cancellationToken
        );

        return TypedResults.NoContent();
    }
}
