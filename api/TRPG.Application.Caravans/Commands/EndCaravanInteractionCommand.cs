using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Routing.Queries;
using TRPG.Data.ModuleContexts;
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
    IRoutingDbContext context,
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
        var travelerExists = await context
            .RouteTravelers.AsNoTracking()
            .AnyAsync(
                traveler => traveler.Id == command.CaravanId && traveler.WorldId == command.WorldId,
                cancellationToken
            );
        if (!travelerExists)
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
