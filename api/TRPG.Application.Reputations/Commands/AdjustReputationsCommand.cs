using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Commands;

public record ReputationAdjustment(Guid TargetId, int DeltaScore);

public class AdjustReputationsCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<ReputationAdjustment> Adjustments { get; init; }
    public required ReputationTargetType TargetType { get; init; }
    public required ReputationReason Reason { get; init; }
    public string? Detail { get; init; }
}

internal class AdjustReputationsCommandHandler(
    IReputationsDbContext context,
    IQueryHandler<GetFactionsByIdsQuery, IReadOnlyDictionary<Guid, Faction>> getFactionsByIds
) : ICommandHandler<AdjustReputationsCommand>
{
    public async Task Handle(
        AdjustReputationsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Adjustments.Count == 0)
        {
            return;
        }

        var deltaScoresByTargetId = command
            .Adjustments.GroupBy(adjustment => adjustment.TargetId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(adjustment => adjustment.DeltaScore)
            );
        var targetIds = deltaScoresByTargetId.Keys.ToArray();

        if (command.TargetType == ReputationTargetType.Faction)
        {
            var factionsById = await getFactionsByIds.Handle(
                new GetFactionsByIdsQuery { Ids = targetIds },
                cancellationToken
            );
            var missingTargetIds = targetIds.Except(factionsById.Keys).ToArray();
            if (missingTargetIds.Length > 0)
            {
                var missingTargetIdsText = string.Join(", ", missingTargetIds);
                throw new InvalidOperationException(
                    $"{command.TargetType} with id(s) {missingTargetIdsText} does not exist."
                );
            }
        }

        var worldId = command.WorldId;

        var existingReputationsByTargetId = await context
            .Reputations.Where(r =>
                r.CreatureId == command.CreatureId
                && r.TargetType == command.TargetType
                && targetIds.AsEnumerable().Contains(r.TargetId)
            )
            .ToDictionaryAsync(r => r.TargetId, cancellationToken);

        foreach (var (targetId, deltaScore) in deltaScoresByTargetId)
        {
            if (existingReputationsByTargetId.TryGetValue(targetId, out var reputation))
            {
                reputation.Score = Math.Clamp(reputation.Score + deltaScore, -100, 100);
            }
            else
            {
                context.Reputations.Add(
                    new Reputation
                    {
                        CreatureId = command.CreatureId,
                        TargetId = targetId,
                        TargetType = command.TargetType,
                        Score = Math.Clamp(deltaScore, -100, 100),
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
                    DeltaScore = deltaScore,
                    Reason = command.Reason,
                    Detail = command.Detail,
                    WorldId = worldId,
                }
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
