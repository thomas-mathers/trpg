using Microsoft.EntityFrameworkCore;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes;

public sealed record PendingCrimeResolution<TCrime>(
    IReadOnlyList<TCrime> Crimes,
    IReadOnlyList<TCrime> ReportedCrimes,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> ReportingWitnessIdsByCrimeId
)
    where TCrime : Crime;

public class PendingCrimeWitnessResolutionService(ICrimesDbContext context)
{
    public async Task<IReadOnlyCollection<Guid>> GetWitnessCandidateCreatureIds<TCrime>(
        Guid worldId,
        Guid playerId,
        Guid locationId,
        CancellationToken cancellationToken
    )
        where TCrime : Crime
    {
        var crimeIds = await context
            .Crimes.OfType<TCrime>()
            .Where(crime =>
                crime.WorldId == worldId
                && crime.PlayerId == playerId
                && crime.LocationId == locationId
                && crime.Resolution == CrimeResolution.Pending
            )
            .Select(crime => crime.Id)
            .ToArrayAsync(cancellationToken);
        if (crimeIds.Length == 0)
        {
            return [];
        }

        return await context
            .CrimeWitnesses.Where(witness =>
                crimeIds.AsEnumerable().Contains(witness.CrimeId)
                && witness.Resolution == CrimeWitnessResolution.Pending
            )
            .Select(witness => witness.CreatureId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PendingCrimeResolution<TCrime>> Resolve<TCrime>(
        Guid worldId,
        Guid playerId,
        Guid locationId,
        IReadOnlyCollection<Guid> liveWitnessCreatureIds,
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

        foreach (var witness in witnesses)
        {
            witness.Resolution = liveWitnessCreatureIds.Contains(witness.CreatureId)
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
