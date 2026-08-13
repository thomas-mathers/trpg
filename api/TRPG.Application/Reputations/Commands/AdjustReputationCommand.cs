using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Reputations.Commands;

internal class AdjustReputationCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid TargetId { get; init; }
    public required ReputationTargetType TargetType { get; init; }
    public required int DeltaScore { get; init; }
}

internal class AdjustReputationCommandHandler(TrpgDbContext context)
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

        if (reputation == null)
        {
            var worldId = await context
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
            reputation.Score = Math.Clamp(
                reputation.Score + command.DeltaScore,
                MinimumScore,
                MaximumScore
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
