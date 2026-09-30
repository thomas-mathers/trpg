using TRPG.Application.Abilities;
using TRPG.Domain;

namespace TRPG.Application.Combat;

internal static class AbilityUsability
{
    public static string? FirstFailure(Combatant player, Ability ability, GameInstant now)
    {
        if (player.IsOnCooldown(ability.Name, now))
        {
            var secondsRemaining = (int)
                Math.Ceiling((player.CooldownReadyAtByAbility[ability.Name] - now).TotalSeconds);
            return $"Ability '{ability.Name}' is on cooldown for {secondsRemaining} more second(s).";
        }

        if (!AbilityGearRequirement.IsMet(player, ability))
        {
            return $"Ability {ability.Name} requires {AbilityGearRequirement.DescribeRequirement(ability)} to be equipped.";
        }

        if (player.CurrentAp < ability.ApCost)
        {
            return $"Ability {ability.Name} costs {ability.ApCost} AP but {player.Name} only has {player.CurrentAp}";
        }

        if (player.CurrentMp < ability.MpCost)
        {
            return $"Ability {ability.Name} costs {ability.MpCost} MP but {player.Name} only has {player.CurrentMp}";
        }

        return null;
    }
}
