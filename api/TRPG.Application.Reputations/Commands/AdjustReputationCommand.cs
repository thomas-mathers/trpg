using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Commands;

public class AdjustReputationCommand
{
    public required Guid CreatureId { get; init; }
    public required IReadOnlyCollection<Guid> TargetIds { get; init; }
    public required ReputationTargetType TargetType { get; init; }
    public required int DeltaScore { get; init; }
    public required ReputationReason Reason { get; init; }
    public string? Detail { get; init; }
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
        if (command.TargetIds.Count == 0)
        {
            return;
        }

        var existingTargetIds =
            command.TargetType == ReputationTargetType.Faction
                ? await context
                    .Factions.Where(f => command.TargetIds.Contains(f.Id))
                    .Select(f => f.Id)
                    .ToArrayAsync(cancellationToken)
                : await context
                    .Creatures.Where(c => command.TargetIds.Contains(c.Id))
                    .Select(c => c.Id)
                    .ToArrayAsync(cancellationToken);

        var missingTargetId = command
            .TargetIds.Except(existingTargetIds)
            .Cast<Guid?>()
            .FirstOrDefault();
        if (missingTargetId != null)
        {
            throw new InvalidOperationException(
                $"{command.TargetType} with id {missingTargetId} does not exist."
            );
        }

        var worldId = await context
            .Creatures.Where(p => p.Id == command.CreatureId)
            .Select(p => p.WorldId)
            .FirstAsync(cancellationToken);

        var existingReputationsByTargetId = await context
            .Reputations.Where(r =>
                r.CreatureId == command.CreatureId
                && r.TargetType == command.TargetType
                && command.TargetIds.Contains(r.TargetId)
            )
            .ToDictionaryAsync(r => r.TargetId, cancellationToken);

        foreach (var targetId in command.TargetIds)
        {
            if (existingReputationsByTargetId.TryGetValue(targetId, out var reputation))
            {
                reputation.Score = Math.Clamp(
                    reputation.Score + command.DeltaScore,
                    MinimumScore,
                    MaximumScore
                );
            }
            else
            {
                context.Reputations.Add(
                    new Reputation
                    {
                        CreatureId = command.CreatureId,
                        TargetId = targetId,
                        TargetType = command.TargetType,
                        Score = Math.Clamp(command.DeltaScore, MinimumScore, MaximumScore),
                        WorldId = worldId,
                    }
                );
            }

            context.ReputationLogEntries.Add(
                new ReputationLogEntry
                {
                    CreatureId = command.CreatureId,
                    TargetId = targetId,
                    TargetType = command.TargetType,
                    DeltaScore = command.DeltaScore,
                    Reason = command.Reason,
                    Detail = command.Detail,
                    WorldId = worldId,
                }
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
