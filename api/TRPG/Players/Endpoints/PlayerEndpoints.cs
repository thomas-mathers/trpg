using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Combat.Mappers;
using TRPG.Combat.Responses;
using ClientCombatantState = TRPG.Combat.Responses.CombatantState;

namespace TRPG.Players.Endpoints;

internal static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        app.MapGet("/players/{playerId:guid}/fight", GetFight).WithName("GetPlayerFight");
        app.MapGet("/players/{playerId:guid}/fight/abilities", GetAbilityAvailability)
            .WithName("GetPlayerFightAbilities");
    }

    private static async Task<
        Results<NotFound, Ok<IReadOnlyCollection<ClientCombatantState>>>
    > GetFight(
        Guid playerId,
        [FromServices]
            IQueryHandler<
            GetActiveFightCombatantsQuery,
            IReadOnlyList<CombatantResult>
        > getCombatants,
        CancellationToken cancellationToken
    )
    {
        var combatants = await getCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = playerId },
            cancellationToken
        );
        if (combatants.Count == 0)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(combatants.ToCombatantStates());
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
