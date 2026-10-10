using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Commands;

public sealed record JourneyCheckpointUpdate(
    Guid JourneyId,
    JourneyStatus Status,
    int LegIndex,
    double LegProgressMeters,
    GameInstant CheckpointedAt,
    GameInstant? PausedAt
);

public class CheckpointJourneysCommand
{
    public required IReadOnlyCollection<JourneyCheckpointUpdate> Updates { get; init; }
}

internal class CheckpointJourneysCommandHandler(IRoutingDbContext context)
    : ICommandHandler<CheckpointJourneysCommand>
{
    public async Task Handle(
        CheckpointJourneysCommand command,
        CancellationToken cancellationToken = default
    )
    {
        foreach (
            var update in command
                .Updates.GroupBy(update => update.JourneyId)
                .Select(group => group.Last())
        )
        {
            await context
                .Journeys.Where(journey => journey.Id == update.JourneyId)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(journey => journey.Status, update.Status)
                            .SetProperty(journey => journey.CheckpointLegIndex, update.LegIndex)
                            .SetProperty(
                                journey => journey.CheckpointLegProgressMeters,
                                update.LegProgressMeters
                            )
                            .SetProperty(journey => journey.CheckpointedAt, update.CheckpointedAt)
                            .SetProperty(journey => journey.PausedAt, update.PausedAt),
                    cancellationToken
                );
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
