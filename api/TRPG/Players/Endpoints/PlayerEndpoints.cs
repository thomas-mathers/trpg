using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Queries;
using TRPG.Combat.Mappers;
using TRPG.Combat.Responses;

namespace TRPG.Players.Endpoints;

internal static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        app.MapGet("/players/{playerId:guid}/abilities/availability", GetAbilityAvailability)
            .WithName("GetPlayerAbilityAvailability");
    }

    private static async Task<Ok<AbilityAvailabilityResponse[]>> GetAbilityAvailability(
        Guid playerId,
        [FromServices]
            IQueryHandler<
            GetAbilityAvailabilityQuery,
            IReadOnlyList<AbilityAvailability>
        > getAbilityAvailability,
        CancellationToken cancellationToken
    )
    {
        var availability = await getAbilityAvailability.Handle(
            new GetAbilityAvailabilityQuery { PlayerId = playerId },
            cancellationToken
        );

        return TypedResults.Ok(availability.Select(ability => ability.ToResponse()).ToArray());
    }
}
