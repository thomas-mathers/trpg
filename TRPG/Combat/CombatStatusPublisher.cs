using Microsoft.AspNetCore.SignalR;
using TRPG.Application.Combat;
using TRPG.Contracts.Combat.Responses;
using TRPG.GameSessions.Hubs;
using ActiveBuff = TRPG.Contracts.Combat.Responses.ActiveBuff;
using ActiveDot = TRPG.Contracts.Combat.Responses.ActiveDot;
using ActiveHot = TRPG.Contracts.Combat.Responses.ActiveHot;
using CombatantState = TRPG.Contracts.Combat.Responses.CombatantState;

namespace TRPG.Combat;

internal class CombatStatusPublisher(
    IHubContext<ChatHub> hubContext,
    WorldConnectionRegistry worldConnections
) : ICombatStatusPublisher
{
    public async Task PublishCombatStatus(
        Guid worldId,
        IReadOnlyList<Combatant> combatants,
        CancellationToken cancellationToken = default
    )
    {
        if (!worldConnections.TryGetConnectionId(worldId, out var connectionId))
        {
            return;
        }

        var state = ToFightState(combatants);

        await hubContext
            .Clients.Client(connectionId!)
            .SendAsync("FightState", state, cancellationToken);
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
                        .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                    ActiveDots: c.ActiveDots.Select(d => new ActiveDot(
                            d.AbilityName,
                            d.Amount,
                            d.DamageType.ToString(),
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
                            b.Attribute.ToString(),
                            b.Amount,
                            b.AmountType.ToString(),
                            b.RemainingTurns
                        ))
                        .ToArray()
                ))
                .ToArray()
        );
}
