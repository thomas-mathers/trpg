using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class ResetAlertedCreaturesCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class ResetAlertedCreaturesCommandHandler(
    IQueryHandler<
        GetCreaturesAtLocationQuery,
        IReadOnlyCollection<CreatureResult>
    > getCreaturesAtLocation,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    LocationCatchUpCache catchUpCache
) : ICommandHandler<ResetAlertedCreaturesCommand>
{
    public async Task Handle(
        ResetAlertedCreaturesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var nearby = await getCreaturesAtLocation.Handle(
            new GetCreaturesAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
            },
            cancellationToken
        );

        var alertedCreatureIds = nearby
            .Where(creature => creature.State == CreatureState.Alerted)
            .Select(creature => creature.Id)
            .ToArray();

        if (alertedCreatureIds.Length == 0)
        {
            return;
        }

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = alertedCreatureIds,
                State = CreatureState.Idle,
            },
            cancellationToken
        );

        var currentDate = GameClock.GetCurrentInGameDate(command.Playtime);

        catchUpCache.Evict(command.WorldId, command.LocationId, currentDate);
    }
}
