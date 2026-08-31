using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Commands;

public class ResolveTheftCrimesCommand
{
    public required Guid LocationId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class ResolveTheftCrimesCommandHandler(
    TrpgDbContext context,
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    ICommandHandler<ApplyReputationPenaltyForTheftsCommand> applyReputationPenaltyForThefts
) : ICommandHandler<ResolveTheftCrimesCommand>
{
    public async Task Handle(
        ResolveTheftCrimesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var resolution = await pendingCrimeWitnessResolution.Resolve<TheftCrime>(
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
            await applyReputationPenaltyForThefts.Handle(
                new ApplyReputationPenaltyForTheftsCommand
                {
                    PlayerId = command.PlayerId,
                    Thefts = resolution
                        .ReportedCrimes.Select(crime => new TheftCrimeReport(
                            crime.Id,
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
