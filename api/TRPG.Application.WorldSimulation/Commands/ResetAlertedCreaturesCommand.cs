using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Commands;

public class ResetAlertedCreaturesCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class ResetAlertedCreaturesCommandHandler(
    IQueryHandler<
        GetCreaturesAtLocationQuery,
        IReadOnlyCollection<CreatureResult>
    > getCreaturesAtLocation,
    ICommandHandler<CalmCreaturesCommand> calmCreatures
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
            .Where(creature => creature.IsAlerted)
            .Select(creature => creature.Id)
            .ToArray();

        if (alertedCreatureIds.Length == 0)
        {
            return;
        }

        await calmCreatures.Handle(
            new CalmCreaturesCommand { CreatureIds = alertedCreatureIds },
            cancellationToken
        );
    }
}
