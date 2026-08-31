using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Commands;

public class ResolveKillCrimesCommand
{
    public required Guid LocationId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class ResolveKillCrimesCommandHandler(
    TrpgDbContext context,
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    ICommandHandler<ApplyReputationPenaltyForKillsCommand> applyReputationPenaltyForKills
) : ICommandHandler<ResolveKillCrimesCommand>
{
    public async Task Handle(
        ResolveKillCrimesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var resolution = await pendingCrimeWitnessResolution.Resolve<KillCrime>(
            command.WorldId,
            command.PlayerId,
            command.LocationId,
            cancellationToken
        );
        if (resolution.Crimes.Count == 0)
        {
            return;
        }

        if (resolution.ReportedCrimes.Count > 0)
        {
            await applyReputationPenaltyForKills.Handle(
                new ApplyReputationPenaltyForKillsCommand
                {
                    KillerId = command.PlayerId,
                    Kills = resolution
                        .ReportedCrimes.Select(crime => new KillCrimeReport(
                            crime.VictimId,
                            resolution.ReportingWitnessIdsByCrimeId[crime.Id]
                        ))
                        .ToArray(),
                },
                cancellationToken
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
