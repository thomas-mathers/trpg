using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TRPG.Commands;
using TRPG.Contracts;
using TRPG.Data;
using TRPG.Extensions;
using TRPG.Generators;
using TRPG.Models;

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
        WorldGenerator worldGenerator,
        CreatureGenerator creatureGenerator,
        BootstrapWorldCommandHandler bootstrapHandler,
        CancellationToken cancellationToken
    )
    {
        var input = new WorldGeneratorInput
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
        };

        var worldResult = await worldGenerator.Generate(input, cancellationToken);

        var playerCreatureType = request.Race.ToCreatureType();
        var homeCountry = worldResult.Countries.First(c => c.DominantRace == playerCreatureType);
        var startingCity = worldResult.Cities.First(c =>
            c.IsCapital && c.CountryId == homeCountry.Id
        );
        var startingState = worldResult.States.First(s => s.Id == startingCity.StateId);
        var startingDistrict = worldResult.Districts.First(d =>
            d.CityId == startingCity.Id && d.DistrictType == DistrictType.CityCenter
        );
        var profession = Enum.Parse<Models.Profession>(request.Profession.ToString());

        var playerResult = creatureGenerator.Generate(
            new CreatureGeneratorInput(
                CreatureType: playerCreatureType,
                Profession: profession,
                WorldId: worldResult.World.Id,
                BirthStateId: startingState.Id,
                StateId: startingState.Id,
                Level: 1,
                Name: request.PlayerName
            )
        );
        playerResult.Creature.CityId = startingCity.Id;
        playerResult.Creature.DistrictId = startingDistrict.Id;

        var result = await bootstrapHandler.Handle(worldResult, playerResult, cancellationToken);

        return Results.Ok(
            new CreateWorldResponse(result.WorldId, result.PlayerId, worldResult.World.Name)
        );
    }

    private static async Task<IResult> ListWorlds(
        TrpgDbContext context,
        CancellationToken cancellationToken
    )
    {
        var worlds = await context
            .Worlds.OrderBy(w => w.Name)
            .Select(w => new WorldSummary(w.Id, w.Name, w.PlayerId != null))
            .ToListAsync(cancellationToken);

        return Results.Ok(worlds);
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
