using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Routing.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Commands;

public class EndCaravanInteractionCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid CaravanId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class EndCaravanInteractionCommandHandler(
    IQueryHandler<GetRouteTravelerIdentityQuery, RouteTravelerIdentity?> getTravelerIdentity,
    IQueryHandler<
        GetRouteTravelerMembersByRouteTravelerIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getMembers,
    ICommandHandler<ReleaseCreaturesCommand> releaseCreatures
) : ICommandHandler<EndCaravanInteractionCommand>
{
    public async Task Handle(
        EndCaravanInteractionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var traveler = await getTravelerIdentity.Handle(
            new GetRouteTravelerIdentityQuery { RouteTravelerId = command.CaravanId },
            cancellationToken
        );
        if (traveler?.WorldId != command.WorldId)
        {
            throw new EntityNotFoundException(nameof(RouteTraveler), command.CaravanId);
        }

        var members = await getMembers.Handle(
            new GetRouteTravelerMembersByRouteTravelerIdsQuery
            {
                RouteTravelerIds = [command.CaravanId],
            },
            cancellationToken
        );
        await releaseCreatures.Handle(
            new ReleaseCreaturesCommand
            {
                WorldId = command.WorldId,
                CreatureIds =
                [
                    command.PlayerId,
                    .. members.GetValueOrDefault(command.CaravanId, []),
                ],
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }
}
