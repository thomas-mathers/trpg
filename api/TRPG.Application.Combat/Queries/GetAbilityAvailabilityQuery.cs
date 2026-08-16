using TRPG.Application.Abilities;
using TRPG.Application.Common.Queries;

namespace TRPG.Application.Combat.Queries;

public record AbilityAvailability(string Name, bool IsUsable, string? Reason);

public class GetAbilityAvailabilityQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetAbilityAvailabilityQueryHandler(ActiveFightCombatantLoader combatantLoader)
    : IQueryHandler<GetAbilityAvailabilityQuery, IReadOnlyList<AbilityAvailability>>
{
    public async Task<IReadOnlyList<AbilityAvailability>> Handle(
        GetAbilityAvailabilityQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var combatants = await combatantLoader.Load(query.PlayerId, cancellationToken);

        var player = combatants.SingleOrDefault(c => c.IsPlayer);
        if (player is null)
        {
            return [];
        }

        return player.Abilities.Select(ability => Evaluate(player, ability)).ToArray();
    }

    private static AbilityAvailability Evaluate(Combatant player, Ability ability)
    {
        var cooldownRemaining = player.CooldownRemainingByAbility.GetValueOrDefault(ability.Name);
        if (cooldownRemaining > 0)
        {
            return new AbilityAvailability(ability.Name, false, $"cooldown {cooldownRemaining}");
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
