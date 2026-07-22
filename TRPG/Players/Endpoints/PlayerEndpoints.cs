using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Mappers;
using TRPG.Contracts.Combat.Responses;
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
    }

    private static async Task<IResult> GetFight(
        Guid playerId,
        GetCombatantsQueryHandler getCombatants,
        CancellationToken cancellationToken
    )
    {
        var combatants = await getCombatants.Handle(
            new GetCombatantsQuery { PlayerId = playerId },
            cancellationToken
        );
        if (combatants.Count == 0)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToFightState(combatants));
    }

    private static FightState ToFightState(IReadOnlyList<Combatant> combatants) =>
        new(
            combatants
                .Select(c => new CombatantState(
                    Name: c.Name,
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
