using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Mappers;
using TRPG.Contracts.Combat.Responses;
using AbilityAvailability = TRPG.Contracts.Combat.Responses.AbilityAvailability;
using ActiveBuff = TRPG.Contracts.Combat.Responses.ActiveBuff;
using ActiveDot = TRPG.Contracts.Combat.Responses.ActiveDot;
using ActiveHot = TRPG.Contracts.Combat.Responses.ActiveHot;
using CombatantState = TRPG.Contracts.Combat.Responses.CombatantState;

namespace TRPG.Players.Endpoints;

internal static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        app.MapGet("/players/{playerId:guid}/fight", GetFight);
        app.MapGet("/players/{playerId:guid}/fight/abilities", GetAbilityAvailability);
    }

    private static async Task<Results<NotFound, Ok<FightState>>> GetFight(
        Guid playerId,
        GetActiveFightCombatantsQueryHandler getCombatants,
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

        return TypedResults.Ok(ToFightState(combatants));
    }

    private static async Task<Ok<AbilityAvailability[]>> GetAbilityAvailability(
        Guid playerId,
        GetAbilityAvailabilityQueryHandler getAbilityAvailability,
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

    internal static FightState ToFightState(IReadOnlyList<Combatant> combatants) =>
        new(
            combatants
                .OrderByDescending(c => c.TurnOrder)
                .Select(c => new CombatantState(
                    Id: c.CreatureId,
                    Name: c.Name,
                    Level: c.Level,
                    IsPlayer: c.IsPlayer,
                    IsAlive: c.IsAlive,
                    CurrentHp: c.CurrentHp,
                    MaximumHp: c.MaximumHp,
                    CurrentAp: c.CurrentAp,
                    MaximumAp: c.MaximumAp,
                    CurrentMp: c.CurrentMp,
                    MaximumMp: c.MaximumMp,
                    ActiveConditions: c.ActiveConditions.Where(kv => kv.Value > 0)
                        .ToDictionary(kv => kv.Key.ToContract(), kv => kv.Value),
                    ActiveDots: c.ActiveDots.Select(d => new ActiveDot(
                            d.AbilityName,
                            d.Amount,
                            d.DamageType.ToContract(),
                            d.RemainingTurns
                        ))
                        .ToArray(),
                    ActiveHots: c.ActiveHots.Select(h => new ActiveHot(
                            h.AbilityName,
                            h.Amount,
                            h.RemainingTurns
                        ))
                        .ToArray(),
                    ActiveBuffs: c.ActiveBuffs.Select(b => new ActiveBuff(
                            b.AbilityName,
                            b.Attribute.ToContract(),
                            b.Amount,
                            b.AmountType.ToContract(),
                            b.RemainingTurns
                        ))
                        .ToArray()
                ))
                .ToArray()
        );
}
