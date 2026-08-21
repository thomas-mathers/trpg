using Microsoft.EntityFrameworkCore;
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
    ICommandHandler<ApplyReputationPenaltyForTheftsCommand> applyReputationPenaltyForThefts
) : ICommandHandler<ResolveTheftCrimesCommand>
{
    public async Task Handle(
        ResolveTheftCrimesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var crimes = await context
            .Crimes.OfType<TheftCrime>()
            .Where(crime =>
                crime.WorldId == command.WorldId
                && crime.PlayerId == command.PlayerId
                && crime.LocationId == command.LocationId
                && crime.Resolution == CrimeResolution.Pending
            )
            .ToArrayAsync(cancellationToken);
        if (crimes.Length == 0)
        {
            return;
        }

        var crimeIds = crimes.Select(crime => crime.Id).ToArray();
        var witnesses = await context
            .CrimeWitnesses.Where(witness =>
                crimeIds.AsEnumerable().Contains(witness.CrimeId)
                && witness.Resolution == CrimeWitnessResolution.Pending
            )
            .ToArrayAsync(cancellationToken);
        var liveWitnessIds = await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == command.WorldId
                && creature.LocationId == command.LocationId
                && creature.State != CreatureState.Dead
            )
            .Select(creature => creature.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var witness in witnesses)
        {
            witness.Resolution = liveWitnessIds.Contains(witness.CreatureId)
                ? CrimeWitnessResolution.Reported
                : CrimeWitnessResolution.Silenced;
            witness.ResolvedAt = DateTime.UtcNow;
        }

        var reportedCrimes = crimes
            .Where(crime =>
                witnesses.Any(witness =>
                    witness.CrimeId == crime.Id
                    && witness.Resolution == CrimeWitnessResolution.Reported
                )
            )
            .ToArray();

        if (reportedCrimes.Length > 0)
        {
            await applyReputationPenaltyForThefts.Handle(
                new ApplyReputationPenaltyForTheftsCommand
                {
                    PlayerId = command.PlayerId,
                    TheftCrimeIds = reportedCrimes.Select(crime => crime.Id).ToArray(),
                },
                cancellationToken
            );
        }

        foreach (var crime in crimes)
        {
            crime.Resolution = reportedCrimes.Contains(crime)
                ? CrimeResolution.Reported
                : CrimeResolution.Unreported;
            crime.ResolvedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
