using TRPG.Application.Combat;
using TRPG.Application.Encounters.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Fights;

internal static class FightPlacement
{
    public static void Apply(
        IReadOnlyList<Combatant> combatants,
        PlayerPlacement? playerPlacement,
        Func<Guid, Point?> findLiveCreaturePosition
    )
    {
        foreach (var combatant in combatants)
        {
            if (!combatant.IsPlayer)
            {
                if (findLiveCreaturePosition(combatant.CreatureId) is { } position)
                {
                    combatant.Position = position;
                }
            }
            else if (playerPlacement is { } placement)
            {
                combatant.Position = placement.Position;
                combatant.Heading = placement.Heading;
            }
        }
    }
}
