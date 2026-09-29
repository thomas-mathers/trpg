using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public record AbilityAvailability(string Name, bool IsUsable, string? Reason);

public class GetAbilityAvailabilityQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetAbilityAvailabilityQueryHandler(
    ActiveFightCombatantLoader combatantLoader,
    IQueryHandler<GetActiveFightQuery, FightEncounter?> getActiveFight,
    IWorldClock worldClock
) : IQueryHandler<GetAbilityAvailabilityQuery, IReadOnlyList<AbilityAvailability>>
{
    public async Task<IReadOnlyList<AbilityAvailability>> Handle(
        GetAbilityAvailabilityQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var fight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = query.PlayerId },
            cancellationToken
        );
        if (fight is null)
        {
            return [];
        }

        var combatants = await combatantLoader.Load(query.PlayerId, cancellationToken);

        var player = combatants.SingleOrDefault(c => c.IsPlayer);
        if (player is null)
        {
            return [];
        }

        var now = await worldClock.GetCurrent(fight.WorldId, cancellationToken);

        return player.Abilities.Select(ability => Evaluate(player, ability, now)).ToArray();
    }

    private static AbilityAvailability Evaluate(Combatant player, Ability ability, GameInstant now)
    {
        if (player.IsOnCooldown(ability.Name, now))
        {
            var secondsRemaining = (int)
                Math.Ceiling((player.CooldownReadyAtByAbility[ability.Name] - now).TotalSeconds);
            return new AbilityAvailability(ability.Name, false, $"cooldown {secondsRemaining}s");
        }

        if (!AbilityGearRequirement.IsMet(player, ability))
        {
            return new AbilityAvailability(
                ability.Name,
                false,
                $"needs {AbilityGearRequirement.DescribeRequirement(ability)}"
            );
        }

        if (player.CurrentAp < ability.ApCost)
        {
            return new AbilityAvailability(ability.Name, false, "not enough AP");
        }

        if (player.CurrentMp < ability.MpCost)
        {
            return new AbilityAvailability(ability.Name, false, "not enough MP");
        }

        return new AbilityAvailability(ability.Name, true, null);
    }
}
