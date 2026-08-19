using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Commands;

public class AdjustReputationCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid TargetId { get; init; }
    public required ReputationTargetType TargetType { get; init; }
    public required int DeltaScore { get; init; }
    public required string Reason { get; init; }
}

internal class AdjustReputationCommandHandler(TrpgDbContext context)
    : ICommandHandler<AdjustReputationCommand>
{
    private const int MinimumScore = -100;
    private const int MaximumScore = 100;

    public async Task Handle(
        AdjustReputationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var targetExists =
            command.TargetType == ReputationTargetType.Faction
                ? await context.Factions.AnyAsync(f => f.Id == command.TargetId, cancellationToken)
                : await context.Creatures.AnyAsync(
                    p => p.Id == command.TargetId,
                    cancellationToken
                );

        if (!targetExists)
        {
            throw new InvalidOperationException(
                $"{command.TargetType} with id {command.TargetId} does not exist."
            );
        }

        var reputation = await context.Reputations.FirstOrDefaultAsync(
            r =>
                r.CreatureId == command.CreatureId
                && r.TargetId == command.TargetId
                && r.TargetType == command.TargetType,
            cancellationToken
        );

        Guid worldId;
        if (reputation == null)
        {
            worldId = await context
                .Creatures.Where(p => p.Id == command.CreatureId)
                .Select(p => p.WorldId)
                .FirstAsync(cancellationToken);
            context.Reputations.Add(
                new Reputation
                {
                    CreatureId = command.CreatureId,
                    TargetId = command.TargetId,
                    TargetType = command.TargetType,
                    Score = Math.Clamp(command.DeltaScore, MinimumScore, MaximumScore),
                    WorldId = worldId,
                }
            );
        }
        else
        {
            worldId = reputation.WorldId;
            reputation.Score = Math.Clamp(
                reputation.Score + command.DeltaScore,
                MinimumScore,
                MaximumScore
            );
        }

        context.ReputationLogEntries.Add(
            new ReputationLogEntry
            {
                CreatureId = command.CreatureId,
                TargetId = command.TargetId,
                TargetType = command.TargetType,
                DeltaScore = command.DeltaScore,
                Reason = command.Reason,
                WorldId = worldId,
            }
        );

        await context.SaveChangesAsync(cancellationToken);
    }
}
