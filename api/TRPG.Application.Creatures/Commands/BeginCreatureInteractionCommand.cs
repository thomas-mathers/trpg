using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class BeginCreatureInteractionCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid CreatureId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class BeginCreatureInteractionCommandHandler(
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<EngageCreaturesCommand> engageCreatures
) : ICommandHandler<BeginCreatureInteractionCommand>
{
    public async Task Handle(
        BeginCreatureInteractionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.PlayerId == command.CreatureId)
        {
            throw new InvalidOperationException("A creature cannot interact with itself.");
        }

        var creatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = [command.PlayerId, command.CreatureId] },
            cancellationToken
        );
        if (!creatures.TryGetValue(command.PlayerId, out var player))
        {
            throw new EntityNotFoundException(nameof(Creature), command.PlayerId);
        }
        if (!creatures.TryGetValue(command.CreatureId, out var creature))
        {
            throw new EntityNotFoundException(nameof(Creature), command.CreatureId);
        }
        if (player.WorldId != command.WorldId || creature.WorldId != command.WorldId)
        {
            throw new InvalidOperationException("Interaction creatures must belong to the world.");
        }
        if (player.LocationId != creature.LocationId)
        {
            throw new InvalidOperationException("Interaction creatures must share a location.");
        }

        await engageCreatures.Handle(
            new EngageCreaturesCommand
            {
                WorldId = command.WorldId,
                CreatureIds = [command.PlayerId, command.CreatureId],
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }
}
