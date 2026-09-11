using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Data.ModuleContexts;
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
            StatFormulas.ApplyPassiveRegen(creature, command.Playtime, optionsSnapshot.Value);
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var creature in creatures)
        {
            context.Entry(creature).State = EntityState.Detached;
        }

        return creatures.ToDictionary(c => c.Id);
    }
}
