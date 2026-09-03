using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public enum WitnessedCrimeKind
{
    Kill,
    Theft,
}

public record WitnessedCrime(
    DateTime OccurredAt,
    WitnessedCrimeKind Kind,
    string SubjectName,
    TheftCrimeOutcome? Outcome
);

public class GetCrimesWitnessedByCreatureQuery
{
    public required Guid WorldId { get; init; }
    public required Guid WitnessCreatureId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class GetCrimesWitnessedByCreatureQueryHandler(ICrimesDbContext context)
    : IQueryHandler<GetCrimesWitnessedByCreatureQuery, IReadOnlyList<WitnessedCrime>>
{
    public async Task<IReadOnlyList<WitnessedCrime>> Handle(
        GetCrimesWitnessedByCreatureQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var kills = await (
            from witness in context.CrimeWitnesses.AsNoTracking()
            where
                witness.WorldId == query.WorldId
                && witness.CreatureId == query.WitnessCreatureId
                && witness.Resolution != CrimeWitnessResolution.Dead
            join crime in context.Crimes.OfType<KillCrime>().AsNoTracking()
                on witness.CrimeId equals crime.Id
            where crime.PlayerId == query.PlayerId
            select new WitnessedCrime(
                crime.OccurredAt,
                WitnessedCrimeKind.Kill,
                crime.VictimName,
                null
            )
        ).ToArrayAsync(cancellationToken);

        var thefts = await (
            from witness in context.CrimeWitnesses.AsNoTracking()
            where
                witness.WorldId == query.WorldId
                && witness.CreatureId == query.WitnessCreatureId
                && witness.Resolution != CrimeWitnessResolution.Dead
            join crime in context.Crimes.OfType<TheftCrime>().AsNoTracking()
                on witness.CrimeId equals crime.Id
            where crime.PlayerId == query.PlayerId
            select new WitnessedCrime(
                crime.OccurredAt,
                WitnessedCrimeKind.Theft,
                crime.OwnerName,
                crime.Outcome
            )
        ).ToArrayAsync(cancellationToken);

        return kills.Concat(thefts).OrderByDescending(crime => crime.OccurredAt).ToArray();
    }
}
