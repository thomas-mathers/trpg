using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Commands;

public class BeginCaravanInteractionCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid CaravanId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class BeginCaravanInteractionCommandHandler(
    IRoutingDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<ResolveRouteTravelerPositionQuery, RouteTimelinePosition?> resolvePosition,
    IQueryHandler<
        GetRouteTravelerMembersByRouteTravelerIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getMembers,
    ICommandHandler<EngageCreaturesCommand> engageCreatures
) : ICommandHandler<BeginCaravanInteractionCommand>
{
    public async Task Handle(
        BeginCaravanInteractionCommand command,
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

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        if (player == null || player.WorldId != command.WorldId)
        {
            throw new EntityNotFoundException(nameof(Creature), command.PlayerId);
        }
        var position = await resolvePosition.Handle(
            new ResolveRouteTravelerPositionQuery
            {
                RouteTravelerId = command.CaravanId,
                GameTime = command.GameTime,
            },
            cancellationToken
        );
        if (
            position is not RouteTimelinePosition.Lingering lingering
            || lingering.LocationId != player.LocationId
        )
        {
            throw new InvalidOperationException(
                "The caravan is not present at the player location."
            );
        }

        var members = await getMembers.Handle(
            new GetRouteTravelerMembersByRouteTravelerIdsQuery
            {
                RouteTravelerIds = [command.CaravanId],
            },
            cancellationToken
        );
        await engageCreatures.Handle(
            new EngageCreaturesCommand
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
