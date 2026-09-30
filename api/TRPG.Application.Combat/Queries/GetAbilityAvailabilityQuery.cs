using TRPG.Application.Abilities;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Queries;

public record AbilityAvailability(string Name, bool IsUsable, string? Reason);

public class GetAbilityAvailabilityQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetAbilityAvailabilityQueryHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    CombatantFactory combatantFactory,
    IWorldClock worldClock
) : IQueryHandler<GetAbilityAvailabilityQuery, IReadOnlyList<AbilityAvailability>>
{
    public async Task<IReadOnlyList<AbilityAvailability>> Handle(
        GetAbilityAvailabilityQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = query.PlayerId },
            cancellationToken
        );
        if (creature is null)
        {
            return [];
        }

        var player = await combatantFactory.Create(creature, true, cancellationToken);
        var now = await worldClock.GetCurrent(creature.WorldId, cancellationToken);

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
