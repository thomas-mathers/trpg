using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations;

internal sealed record PendingCrimeResolution<TCrime>(
    IReadOnlyList<TCrime> Crimes,
    IReadOnlyList<TCrime> ReportedCrimes,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> ReportingWitnessIdsByCrimeId
)
    where TCrime : Crime;

internal class PendingCrimeWitnessResolutionService(TrpgDbContext context)
{
    public async Task<PendingCrimeResolution<TCrime>> Resolve<TCrime>(
        Guid worldId,
        Guid playerId,
        Guid locationId,
        CancellationToken cancellationToken
    )
        where TCrime : Crime
    {
        var crimes = await context
            .Crimes.OfType<TCrime>()
            .Where(crime =>
                crime.WorldId == worldId
                && crime.PlayerId == playerId
                && crime.LocationId == locationId
                && crime.Resolution == CrimeResolution.Pending
            )
            .ToArrayAsync(cancellationToken);
        if (crimes.Length == 0)
        {
            return new PendingCrimeResolution<TCrime>(
                [],
                [],
                new Dictionary<Guid, IReadOnlyList<Guid>>()
            );
        }

        var crimeIds = crimes.Select(crime => crime.Id).ToArray();
        var witnesses = await context
            .CrimeWitnesses.Where(witness =>
                crimeIds.AsEnumerable().Contains(witness.CrimeId)
                && witness.Resolution == CrimeWitnessResolution.Pending
            )
            .ToArrayAsync(cancellationToken);
        var witnessCreatureIds = witnesses
            .Select(witness => witness.CreatureId)
            .Distinct()
            .ToArray();
        var liveWitnessIds = await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == worldId
                && witnessCreatureIds.AsEnumerable().Contains(creature.Id)
                && creature.State != CreatureState.Dead
            )
            .Select(creature => creature.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var witness in witnesses)
        {
            witness.Resolution = liveWitnessIds.Contains(witness.CreatureId)
                ? CrimeWitnessResolution.Reported
                : CrimeWitnessResolution.Dead;
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

        var reportingWitnessIdsByCrimeId = reportedCrimes.ToDictionary(
            crime => crime.Id,
            IReadOnlyList<Guid> (crime) =>
                witnesses
                    .Where(witness =>
                        witness.CrimeId == crime.Id
                        && witness.Resolution == CrimeWitnessResolution.Reported
                    )
                    .Select(witness => witness.CreatureId)
                    .ToArray()
        );

        foreach (var crime in crimes)
        {
            crime.Resolution = reportedCrimes.Contains(crime)
                ? CrimeResolution.Reported
                : CrimeResolution.Unreported;
            crime.ResolvedAt = DateTime.UtcNow;
        }

        return new PendingCrimeResolution<TCrime>(
            crimes,
            reportedCrimes,
            reportingWitnessIdsByCrimeId
        );
    }
}
