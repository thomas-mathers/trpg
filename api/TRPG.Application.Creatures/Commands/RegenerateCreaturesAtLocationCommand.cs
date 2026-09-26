using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Mappers;
using TRPG.Application.Creatures.Results;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class RegenerateCreaturesAtLocationCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required GameInstant GameTime { get; init; }
    public IReadOnlyCollection<Guid> ExcludedCreatureIds { get; init; } = [];
}

internal class RegenerateCreaturesAtLocationCommandHandler(
    ICreaturesDbContext context,
    IOptionsSnapshot<CreatureRegenOptions> optionsSnapshot
) : ICommandHandler<RegenerateCreaturesAtLocationCommand, IReadOnlyCollection<CreatureVitals>>
{
    public async Task<IReadOnlyCollection<CreatureVitals>> Handle(
        RegenerateCreaturesAtLocationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creatures = await context
            .Creatures.Where(creature =>
                creature.WorldId == command.WorldId
                && creature.LocationId == command.LocationId
                && creature.State != CreatureState.Dead
                && (
                    creature.CurrentHp < creature.MaximumHp
                    || creature.CurrentAp < creature.MaximumAp
                    || creature.CurrentMp < creature.MaximumMp
                )
                && !command.ExcludedCreatureIds.AsEnumerable().Contains(creature.Id)
            )
            .ToArrayAsync(cancellationToken);

        var changedVitals = new List<CreatureVitals>();
        foreach (var creature in creatures)
        {
            var before = creature.ToVitals();
            StatFormulas.ApplyPassiveRegen(creature, command.GameTime, optionsSnapshot.Value);

            var after = creature.ToVitals();
            if (after != before)
            {
                changedVitals.Add(after);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var creature in creatures)
        {
            context.Entry(creature).State = EntityState.Detached;
        }

        return changedVitals.ToArray();
    }
}
