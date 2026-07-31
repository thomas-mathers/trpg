using TRPG.Application.Abilities;

namespace TRPG.Application.Worlds.Generators;

internal static class AbilityExtensions
{
    public static string RandomAttackAbility(this IEnumerable<Ability> abilities)
    {
        var attacks = abilities.OfType<AttackAbility>().ToList();
        return attacks[Random.Shared.Next(attacks.Count)].Name;
    }
}
