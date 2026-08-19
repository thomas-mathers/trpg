using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Commands;

public class ApplyReputationPenaltyForKillsCommand
{
    public required Guid KillerId { get; init; }
    public required IReadOnlyCollection<Guid> KilledCreatureIds { get; init; }
}

internal class ApplyReputationPenaltyForKillsCommandHandler(
    TrpgDbContext context,
    ICommandHandler<AdjustReputationCommand> adjustReputation,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICommandHandler<ApplyReputationPenaltyForKillsCommand>
{
    public async Task Handle(
        ApplyReputationPenaltyForKillsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.KilledCreatureIds.Count == 0)
        {
            return;
        }

        var killedFactionIds = await context
            .FactionMembers.AsNoTracking()
            .Where(m => command.KilledCreatureIds.Contains(m.CreatureId))
            .Select(m => m.FactionId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        foreach (var factionId in killedFactionIds)
        {
            await adjustReputation.Handle(
                new AdjustReputationCommand
                {
                    CreatureId = command.KillerId,
                    TargetId = factionId,
                    TargetType = ReputationTargetType.Faction,
                    DeltaScore = reputationOptions.CurrentValue.KillReputationPenalty,
                    Reason = "Killed a member of this faction",
                },
                cancellationToken
            );
        }
    }
}
