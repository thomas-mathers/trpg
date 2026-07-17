using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.GameSessions;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Commands;

internal class ApplyPassiveRegenCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class ApplyPassiveRegenCommandHandler(
    TrpgDbContext context,
    IOptionsSnapshot<CreatureRegenOptions> optionsSnapshot
)
{
    public async Task Handle(
        ApplyPassiveRegenCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CreatureIds.Count == 0)
        {
            return;
        }

        var creatures = await context
            .Creatures.Where(c => command.CreatureIds.Contains(c.Id))
            .ToArrayAsync(cancellationToken);

        var equippedItems = await context
            .InventoryItems.AsNoTracking()
            .Include(i => i.Item)
            .Where(i => command.CreatureIds.Contains(i.CreatureId) && i.EquippedSlot != null)
            .ToArrayAsync(cancellationToken);
        var equippedByCreatureId = equippedItems
            .GroupBy(i => i.CreatureId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyCollection<Item>)g.Select(i => i.Item).ToArray()
            );

        foreach (var creature in creatures)
        {
            var equipped = equippedByCreatureId.GetValueOrDefault(creature.Id, []);
            var effectiveAttributes = StatFormulas.CalculateEffectiveAttributes(
                creature.Attributes,
                [],
                equipped
            );
            ApplyPassiveRegen(
                creature,
                command.Playtime,
                effectiveAttributes,
                optionsSnapshot.Value
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyPassiveRegen(
        Creature creature,
        TimeSpan currentPlaytime,
        Attributes effectiveAttributes,
        CreatureRegenOptions options
    )
    {
        if (creature.State == CreatureState.Dead)
        {
            return;
        }

        var elapsedInGameHours =
            (currentPlaytime - creature.LastRegenPlaytime).TotalHours
            / GameClock.RealTimePerInGameHour.TotalHours;
        if (elapsedInGameHours <= 0)
        {
            return;
        }

        creature.CurrentHp = Regen(
            creature.CurrentHp,
            effectiveAttributes.MaximumHp,
            options.HpRegenPercentPerHour,
            elapsedInGameHours
        );
        creature.CurrentAp = Regen(
            creature.CurrentAp,
            effectiveAttributes.MaximumAp,
            options.ApRegenPercentPerHour,
            elapsedInGameHours
        );
        creature.CurrentMp = Regen(
            creature.CurrentMp,
            effectiveAttributes.MaximumMp,
            options.MpRegenPercentPerHour,
            elapsedInGameHours
        );
        creature.LastRegenPlaytime = currentPlaytime;
    }

    private static int Regen(int current, int maximum, float percentPerHour, double elapsedHours) =>
        Math.Min(maximum, current + (int)Math.Round(maximum * percentPerHour * elapsedHours));
}
