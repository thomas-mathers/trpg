using TRPG.Application.Combat.Events;
using CombatRoundEvent = TRPG.Application.Combat.Responses.CombatRoundEvent;

namespace TRPG.Application.Combat.Mappers;

internal static class CombatRoundEventMapper
{
    public static IReadOnlyList<CombatRoundEvent> ToCombatRoundEvents(
        this IReadOnlyList<CombatEvent> events
    ) =>
        events.Select(combatEvent => combatEvent.ToContract()).OfType<CombatRoundEvent>().ToArray();

    private static CombatRoundEvent? ToContract(this CombatEvent combatEvent) =>
        combatEvent switch
        {
            Hit hit => hit.ToContract(),
            Miss miss => miss.ToContract(),
            Block block => block.ToContract(),
            Regenerated regenerated => regenerated.ToContract(),
            ResourceStateUpdated resourceStateUpdated => resourceStateUpdated.ToContract(),
            _ => null,
        };
}
