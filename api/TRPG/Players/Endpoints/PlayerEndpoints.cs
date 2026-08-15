using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Handling;
using TRPG.Contracts.Combat.Responses;
using AbilityAvailability = TRPG.Contracts.Combat.Responses.AbilityAvailability;

namespace TRPG.Players.Endpoints;

internal static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        app.MapGet("/players/{playerId:guid}/fight", GetFight).WithName("GetPlayerFight");
        app.MapGet("/players/{playerId:guid}/fight/abilities", GetAbilityAvailability)
            .WithName("GetPlayerFightAbilities");
    }

    private static async Task<Results<NotFound, Ok<IReadOnlyCollection<CombatantState>>>> GetFight(
        Guid playerId,
        [FromServices]
            IQueryHandler<
            GetActiveFightCombatantDetailsQuery,
            IReadOnlyCollection<CombatantState>
        > getCombatants,
        CancellationToken cancellationToken
    )
    {
        var combatants = await getCombatants.Handle(
            new GetActiveFightCombatantDetailsQuery { PlayerId = playerId },
            cancellationToken
        );
        if (combatants.Count == 0)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(combatants);
    }

    private static async Task<Ok<AbilityAvailability[]>> GetAbilityAvailability(
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

        return TypedResults.Ok(
            availability
                .Select(a => new AbilityAvailability(a.Name, a.IsUsable, a.Reason))
                .ToArray()
        );
    }
}
