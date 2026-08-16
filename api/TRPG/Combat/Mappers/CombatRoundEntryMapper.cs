using TRPG.Application.Combat.Events;
using CombatRoundEntry = TRPG.Combat.ClientModels.CombatRoundEntry;

namespace TRPG.Combat.Mappers;

internal static class CombatRoundEntryMapper
{
    public static IReadOnlyList<CombatRoundEntry> ToCombatRoundEntries(
        this IReadOnlyList<CombatResolution> events
    ) =>
        events.Select(combatEvent => combatEvent.ToContract()).OfType<CombatRoundEntry>().ToArray();

    private static CombatRoundEntry? ToContract(this CombatResolution combatEvent) =>
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
