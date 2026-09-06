using Microsoft.EntityFrameworkCore;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes;

public sealed record PendingCrimeResolution<TCrime>(
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
            .AsNoTracking()
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
            .CrimeWitnesses.AsNoTracking()
            .Where(witness =>
                crimeIds.AsEnumerable().Contains(witness.CrimeId)
                && witness.Resolution == CrimeWitnessResolution.Pending
            )
            .Select(witness => witness.CreatureId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    // Reads and writes stay untracked so a caller can never price a crime from a stale field.
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
            .AsNoTracking()
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
                new Dictionary<Guid, IReadOnlyList<Guid>>()
            );
        }

        var crimeIds = crimes.Select(crime => crime.Id).ToArray();
        var witnesses = await context
            .CrimeWitnesses.AsNoTracking()
            .Where(witness =>
                crimeIds.AsEnumerable().Contains(witness.CrimeId)
                && witness.Resolution == CrimeWitnessResolution.Pending
            )
            .Select(witness => new { witness.CrimeId, witness.CreatureId })
            .ToArrayAsync(cancellationToken);

        var reportingWitnessIdsByCrimeId = witnesses
            .Where(witness => liveWitnessCreatureIds.Contains(witness.CreatureId))
            .GroupBy(witness => witness.CrimeId)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<Guid> (group) => group.Select(witness => witness.CreatureId).ToArray()
            );

        var reportedCrimeIds = reportingWitnessIdsByCrimeId.Keys.ToArray();
        var resolvedAt = DateTime.UtcNow;

        await ResolveWitnesses(crimeIds, liveWitnessCreatureIds, resolvedAt, cancellationToken);
        await ResolveCrimes<TCrime>(crimeIds, reportedCrimeIds, resolvedAt, cancellationToken);

        var reportedCrimes = crimes
            .Where(crime => reportingWitnessIdsByCrimeId.ContainsKey(crime.Id))
            .ToArray();

        return new PendingCrimeResolution<TCrime>(reportedCrimes, reportingWitnessIdsByCrimeId);
    }

    private async Task ResolveWitnesses(
        IReadOnlyCollection<Guid> crimeIds,
        IReadOnlyCollection<Guid> liveWitnessCreatureIds,
        DateTime resolvedAt,
        CancellationToken cancellationToken
    )
    {
        await context
            .CrimeWitnesses.Where(witness =>
                crimeIds.AsEnumerable().Contains(witness.CrimeId)
                && witness.Resolution == CrimeWitnessResolution.Pending
                && liveWitnessCreatureIds.AsEnumerable().Contains(witness.CreatureId)
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(witness => witness.Resolution, CrimeWitnessResolution.Reported)
                        .SetProperty(witness => witness.ResolvedAt, resolvedAt),
                cancellationToken
            );

        await context
            .CrimeWitnesses.Where(witness =>
                crimeIds.AsEnumerable().Contains(witness.CrimeId)
                && witness.Resolution == CrimeWitnessResolution.Pending
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(witness => witness.Resolution, CrimeWitnessResolution.Dead)
                        .SetProperty(witness => witness.ResolvedAt, resolvedAt),
                cancellationToken
            );
    }

    private async Task ResolveCrimes<TCrime>(
        IReadOnlyCollection<Guid> crimeIds,
        IReadOnlyCollection<Guid> reportedCrimeIds,
        DateTime resolvedAt,
        CancellationToken cancellationToken
    )
        where TCrime : Crime
    {
        if (reportedCrimeIds.Count > 0)
        {
            await context
                .Crimes.OfType<TCrime>()
                .Where(crime => reportedCrimeIds.AsEnumerable().Contains(crime.Id))
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(crime => crime.Resolution, CrimeResolution.Reported)
                            .SetProperty(crime => crime.ResolvedAt, resolvedAt),
                    cancellationToken
                );
        }

        await context
            .Crimes.OfType<TCrime>()
            .Where(crime =>
                crimeIds.AsEnumerable().Contains(crime.Id)
                && !reportedCrimeIds.AsEnumerable().Contains(crime.Id)
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(crime => crime.Resolution, CrimeResolution.Unreported)
                        .SetProperty(crime => crime.ResolvedAt, resolvedAt),
                cancellationToken
            );
    }
}
