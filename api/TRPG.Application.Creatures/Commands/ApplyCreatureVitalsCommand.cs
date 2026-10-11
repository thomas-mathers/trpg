using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Results;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public sealed record CreatureVitalsWrite(CreatureVitals Vitals, GameInstant LastRegenAt);

public class ApplyCreatureVitalsCommand
{
    public required IReadOnlyCollection<CreatureVitalsWrite> Writes { get; init; }
}

internal class ApplyCreatureVitalsCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<ApplyCreatureVitalsCommand>
{
    public async Task Handle(
        ApplyCreatureVitalsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var write in command.Writes)
        {
            var vitals = write.Vitals;
            await context
                .Creatures.Where(creature =>
                    creature.Id == vitals.CreatureId && creature.Condition != CreatureCondition.Dead
                )
                .ExecuteUpdateAsync(
                    update =>
                        update
                            .SetProperty(creature => creature.CurrentHp, vitals.CurrentHp)
                            .SetProperty(creature => creature.CurrentAp, vitals.CurrentAp)
                            .SetProperty(creature => creature.CurrentMp, vitals.CurrentMp)
                            .SetProperty(creature => creature.LastRegenGameTime, write.LastRegenAt),
                    cancellationToken
                );
        }
    }
}
