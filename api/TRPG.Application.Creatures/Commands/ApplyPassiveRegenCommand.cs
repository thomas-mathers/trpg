using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class ApplyPassiveRegenCommand
{
    public required TimeSpan Playtime { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class ApplyPassiveRegenCommandHandler(
    ICreaturesDbContext context,
    IOptionsSnapshot<CreatureRegenOptions> optionsSnapshot
) : ICommandHandler<ApplyPassiveRegenCommand, IReadOnlyDictionary<Guid, Creature>>
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

        var creatures = await context
            .Creatures.Where(c => command.CreatureIds.Contains(c.Id))
            .ToArrayAsync(cancellationToken);

        foreach (var creature in creatures)
        {
            ApplyPassiveRegen(creature, command.Playtime, optionsSnapshot.Value);
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
            creature.MaximumHp,
            options.HpRegenPercentPerHour,
            elapsedInGameHours
        );
        creature.CurrentAp = Regen(
            creature.CurrentAp,
            creature.MaximumAp,
            options.ApRegenPercentPerHour,
            elapsedInGameHours
        );
        creature.CurrentMp = Regen(
            creature.CurrentMp,
            creature.MaximumMp,
            options.MpRegenPercentPerHour,
            elapsedInGameHours
        );
        creature.LastRegenPlaytime = currentPlaytime;
    }

    private static int Regen(int current, int maximum, float percentPerHour, double elapsedHours) =>
        Math.Min(maximum, current + (int)Math.Round(maximum * percentPerHour * elapsedHours));
}
