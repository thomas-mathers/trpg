using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Contracts;
using TRPG.Extensions;
using Profession = TRPG.Data.Models.Profession;

namespace TRPG.Endpoints;

internal static class WorldEndpoints
{
    public static void MapWorldEndpoints(this WebApplication app)
    {
        app.MapPost("/worlds", CreateWorld);
        app.MapGet("/worlds", ListWorlds);
        app.MapDelete("/worlds/{worldId:guid}", DropWorld);
    }

    private static async Task<IResult> CreateWorld(
        CreateWorldRequest request,
        CreateWorldCommandHandler createWorld,
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
            PlayerCreatureType = request.Race.ToCreatureType(),
            PlayerProfession = Enum.Parse<Profession>(request.Profession.ToString()),
            PlayerName = request.PlayerName,
        };

        var result = await createWorld.Handle(command, cancellationToken);

        return Results.Ok(
            new CreateWorldResponse(result.WorldId, result.PlayerId, result.WorldName)
        );
    }

    private static async Task<IResult> ListWorlds(
        ListWorldsQueryHandler listWorlds,
        CancellationToken cancellationToken
    )
    {
        var worlds = await listWorlds.Handle(new ListWorldsQuery(), cancellationToken);
        return Results.Ok(
            worlds.Select(w => new WorldSummary(w.Id, w.Name, w.PlayerId != null)).ToArray()
        );
    }

    private static async Task<IResult> DropWorld(
        Guid worldId,
        DropWorldCommandHandler dropHandler,
        CancellationToken cancellationToken
    )
    {
        await dropHandler.Handle(new DropWorldCommand { WorldId = worldId }, cancellationToken);
        return Results.NoContent();
    }
}
