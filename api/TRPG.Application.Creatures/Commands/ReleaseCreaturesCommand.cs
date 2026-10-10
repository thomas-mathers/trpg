using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class ReleaseCreaturesCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class ReleaseCreaturesCommandHandler(
    ICreaturesDbContext context,
    IDomainEventPublisher<CreaturesReleasedEvent> publisher
) : ICommandHandler<ReleaseCreaturesCommand>
{
    public async Task Handle(
        ReleaseCreaturesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creatureIds = command.CreatureIds.Distinct().ToArray();
        if (creatureIds.Length == 0)
        {
            return;
        }

        var creatures = await context
            .Creatures.Where(creature =>
                creature.WorldId == command.WorldId
                && creatureIds.AsEnumerable().Contains(creature.Id)
            )
            .ToArrayAsync(cancellationToken);
        var missingCreatureId = creatureIds.FirstOrDefault(id => creatures.All(c => c.Id != id));
        if (missingCreatureId != Guid.Empty)
        {
            throw new EntityNotFoundException(nameof(Creature), missingCreatureId);
        }

        var releasedIds = creatures
            .Where(creature => creature.IsEngaged)
            .Select(creature => creature.Id)
            .ToArray();
        if (releasedIds.Length == 0)
        {
            return;
        }

        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        foreach (var creature in creatures.Where(creature => creature.IsEngaged))
        {
            creature.IsEngaged = false;
        }
        await context.SaveChangesAsync(cancellationToken);
        await publisher.Publish(
            new CreaturesReleasedEvent(command.WorldId, releasedIds, command.GameTime),
            cancellationToken
        );
        transaction.Complete();
    }
}
