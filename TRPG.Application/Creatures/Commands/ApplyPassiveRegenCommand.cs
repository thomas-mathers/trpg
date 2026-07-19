using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Commands;

internal class ApplyPassiveRegenCommand
{
    public required Guid SessionId { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class ApplyPassiveRegenCommandHandler(
    TrpgDbContext context,
    IOptionsSnapshot<CreatureRegenOptions> optionsSnapshot,
    GetPlaytimeQueryHandler getPlaytime
)
{
    public async Task<IReadOnlyDictionary<Guid, Creature>> Handle(
        ApplyPassiveRegenCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CreatureIds.Count == 0)
        {
            return new Dictionary<Guid, Creature>();
        }

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

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
                IReadOnlyCollection<Item> (g) => g.Select(i => i.Item).ToArray()
            );

        foreach (var creature in creatures)
        {
            var equipped = equippedByCreatureId.GetValueOrDefault(creature.Id, []);
            var effectiveAttributes = StatFormulas.CalculateEffectiveAttributes(
                creature.Attributes,
                [],
                equipped
            );
            ApplyPassiveRegen(creature, playtime, effectiveAttributes, optionsSnapshot.Value);
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var creature in creatures)
        {
            context.Entry(creature).State = EntityState.Detached;
        }

        return creatures.ToDictionary(c => c.Id);
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
