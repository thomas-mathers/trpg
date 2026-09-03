using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public enum WitnessedCrimeKind
{
    Kill,
    Theft,
    BreakingAndEntering,
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
        var crimes = await (
            from witness in context.CrimeWitnesses.AsNoTracking()
            where
                witness.WorldId == query.WorldId
                && witness.CreatureId == query.WitnessCreatureId
                && witness.Resolution != CrimeWitnessResolution.Dead
            join crime in context.Crimes.AsNoTracking() on witness.CrimeId equals crime.Id
            where crime.PlayerId == query.PlayerId
            select crime
        ).ToArrayAsync(cancellationToken);

        return crimes
            .Select(ToWitnessedCrime)
            .OrderByDescending(crime => crime.OccurredAt)
            .ToArray();
    }

    private static WitnessedCrime ToWitnessedCrime(Crime crime) =>
        crime switch
        {
            KillCrime kill => new WitnessedCrime(
                kill.OccurredAt,
                WitnessedCrimeKind.Kill,
                kill.VictimName,
                null
            ),
            TheftCrime theft => new WitnessedCrime(
                theft.OccurredAt,
                WitnessedCrimeKind.Theft,
                theft.OwnerName,
                theft.Outcome
            ),
            BreakingAndEnteringCrime breakIn => new WitnessedCrime(
                breakIn.OccurredAt,
                WitnessedCrimeKind.BreakingAndEntering,
                breakIn.BuildingName,
                null
            ),
            _ => throw new InvalidOperationException($"Unhandled crime type {crime.GetType()}"),
        };
}
