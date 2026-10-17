using TRPG.Application.Abilities;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat;

internal sealed record ValidatedAbilityUse(Ability Ability, IReadOnlyList<Combatant> Targets);

internal sealed record AbilityUseValidation(
    ValidatedAbilityUse? Use,
    AbilityUseRejection? Rejection,
    string? Message,
    double? Reach = null
)
{
    public static AbilityUseValidation Valid(ValidatedAbilityUse use) => new(use, null, null);

    public static AbilityUseValidation Invalid(AbilityUseRejection rejection, string message) =>
        new(null, rejection, message);

    public static AbilityUseValidation OutOfRange(string message, double reach) =>
        new(null, AbilityUseRejection.OutOfRange, message, reach);
}

internal static class AbilityUseValidator
{
    public const double RangeTolerance = 0.25;

    public static AbilityUseValidation Validate(
        IReadOnlyList<Combatant> combatants,
        Combatant actor,
        Guid targetId,
        string abilityName,
        GameInstant now
    )
    {
        var ability = actor.Abilities.FirstOrDefault(a => a.Name == abilityName);
        if (ability is null)
        {
            return Invalid(AbilityUseRejection.NotFound, $"Ability {abilityName} not found");
        }

        var target = combatants.FirstOrDefault(c => c.CreatureId == targetId);
        var targetFailure = ValidateTarget(actor, ability, target);
        if (targetFailure is not null)
        {
            return targetFailure;
        }

        var usabilityFailure = ValidateUsability(actor, ability, now);
        if (usabilityFailure is not null)
        {
            return usabilityFailure;
        }

        var reachFailure = ValidateReach(actor, ability, target!);
        if (reachFailure is not null)
        {
            return reachFailure;
        }

        return AbilityUseValidation.Valid(
            new ValidatedAbilityUse(ability, ResolveTargets(combatants, actor, ability, target!))
        );
    }

    private static AbilityUseValidation Invalid(AbilityUseRejection rejection, string message) =>
        AbilityUseValidation.Invalid(rejection, message);

    private static AbilityUseValidation? ValidateTarget(
        Combatant actor,
        Ability ability,
        Combatant? target
    )
    {
        if (target is null)
        {
            return Invalid(AbilityUseRejection.InvalidTarget, "That target is not here.");
        }

        if (!target.IsAlive)
        {
            return Invalid(AbilityUseRejection.InvalidTarget, $"{target.Name} is already dead");
        }

        return (ability, target == actor) switch
        {
            (AttackAbility, true) => Invalid(
                AbilityUseRejection.InvalidTarget,
                $"Ability {ability.Name} cannot target {actor.Name}"
            ),
            (AttackAbility, false) when target.IsPlayer == actor.IsPlayer => Invalid(
                AbilityUseRejection.InvalidTarget,
                $"Ability {ability.Name} cannot target an ally"
            ),
            _ => null,
        };
    }

    private static AbilityUseValidation? ValidateUsability(
        Combatant actor,
        Ability ability,
        GameInstant now
    )
    {
        var failure = AbilityUsability.FirstFailure(actor, ability, now);
        if (failure is not null)
        {
            return Invalid(AbilityUseRejection.Unusable, failure);
        }

        return ability.TriggersGlobalCooldown && actor.GlobalCooldownReadyAt > now
            ? Invalid(AbilityUseRejection.GlobalCooldown, "You can't use another ability yet.")
            : null;
    }

    private static AbilityUseValidation? ValidateReach(
        Combatant actor,
        Ability ability,
        Combatant target
    )
    {
        if (target == actor)
        {
            return null;
        }

        var range = ability.EffectiveRange;
        var distance = Distance(actor.Position, target.Position);
        if (distance > range.Max + RangeTolerance)
        {
            return AbilityUseValidation.OutOfRange($"{target.Name} is out of range.", range.Max);
        }

        if (distance < range.Min)
        {
            return Invalid(AbilityUseRejection.TooClose, $"{target.Name} is too close.");
        }

        return
            ability.RequiresFacing
            && !AbilityFacing.IsFacing(actor.Position, actor.Heading, target.Position)
            ? Invalid(AbilityUseRejection.NotFacing, $"You are not facing {target.Name}.")
            : null;
    }

    private static IReadOnlyList<Combatant> ResolveTargets(
        IReadOnlyList<Combatant> combatants,
        Combatant actor,
        Ability ability,
        Combatant target
    ) =>
        ability switch
        {
            AttackAbility { TargetType: AttackTargetType.Aoe } => combatants
                .Where(c => c.IsAlive && c.IsPlayer != actor.IsPlayer)
                .ToArray(),
            SupportAbility { TargetType: TargetType.Aoe or TargetType.Self } => [actor],
            _ => [target],
        };

    private static double Distance(Point from, Point to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;

        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
