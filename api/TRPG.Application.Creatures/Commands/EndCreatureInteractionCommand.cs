using TRPG.Application.Common.Commands;
using TRPG.Domain;

namespace TRPG.Application.Creatures.Commands;

public class EndCreatureInteractionCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid CreatureId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class EndCreatureInteractionCommandHandler(
    ICommandHandler<ReleaseCreaturesCommand> releaseCreatures
) : ICommandHandler<EndCreatureInteractionCommand>
{
    public Task Handle(
        EndCreatureInteractionCommand command,
        CancellationToken cancellationToken = default
    ) =>
        releaseCreatures.Handle(
            new ReleaseCreaturesCommand
            {
                WorldId = command.WorldId,
                CreatureIds = [command.PlayerId, command.CreatureId],
                GameTime = command.GameTime,
            },
            cancellationToken
        );
}
