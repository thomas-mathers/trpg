using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class EngageCreaturesCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class EngageCreaturesCommandHandler(
    ICreaturesDbContext context,
    IDomainEventPublisher<CreaturesEngagedEvent> publisher
) : ICommandHandler<EngageCreaturesCommand>
{
    public async Task Handle(
        EngageCreaturesCommand command,
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
        if (creatures.Any(creature => creature.State == CreatureState.Dead))
        {
            throw new InvalidOperationException("A dead creature cannot be engaged.");
        }
        if (creatures.Any(creature => creature.IsEngaged))
        {
            throw new InvalidOperationException("A creature is already engaged.");
        }

        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        foreach (var creature in creatures)
        {
            creature.IsEngaged = true;
        }
        await context.SaveChangesAsync(cancellationToken);
        await publisher.Publish(
            new CreaturesEngagedEvent(command.WorldId, creatureIds, command.GameTime),
            cancellationToken
        );
        transaction.Complete();
    }
}
