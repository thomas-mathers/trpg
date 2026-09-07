using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public enum WitnessedCrimeKind
{
    Kill,
    Assault,
    Theft,
    Lockpicking,
    Trespassing,
}

public record WitnessedCrime(
    DateTime OccurredAt,
    WitnessedCrimeKind Kind,
    string SubjectName,
    Guid? SubjectCreatureId,
    TheftCrimeOutcome? Outcome,
    CrimeWitnessKind Awareness
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
            select new { Crime = crime, witness.Kind }
        ).ToArrayAsync(cancellationToken);

        return crimes
            .Select(entry => ToWitnessedCrime(entry.Crime, entry.Kind))
            .OrderByDescending(crime => crime.OccurredAt)
            .ToArray();
    }

    private static WitnessedCrime ToWitnessedCrime(Crime crime, CrimeWitnessKind awareness) =>
        crime switch
        {
            KillCrime kill => new WitnessedCrime(
                kill.OccurredAt,
                WitnessedCrimeKind.Kill,
                kill.VictimName,
                kill.VictimId,
                null,
                awareness
            ),
            AssaultCrime assault => new WitnessedCrime(
                assault.OccurredAt,
                WitnessedCrimeKind.Assault,
                assault.VictimName,
                assault.VictimId,
                null,
                awareness
            ),
            TheftCrime theft => new WitnessedCrime(
                theft.OccurredAt,
                WitnessedCrimeKind.Theft,
                theft.OwnerName,
                theft.OwnerCreatureId,
                theft.Outcome,
                awareness
            ),
            LockpickingCrime breakIn => new WitnessedCrime(
                breakIn.OccurredAt,
                WitnessedCrimeKind.Lockpicking,
                breakIn.BuildingName,
                null,
                null,
                awareness
            ),
            TrespassingCrime trespass => new WitnessedCrime(
                trespass.OccurredAt,
                WitnessedCrimeKind.Trespassing,
                trespass.BuildingName,
                null,
                null,
                awareness
            ),
            _ => throw new InvalidOperationException($"Unhandled crime type {crime.GetType()}"),
        };
}
