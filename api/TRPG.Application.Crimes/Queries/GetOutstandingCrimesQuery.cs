using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public enum OutstandingCrimeKind
{
    Kill,
    Assault,
    Theft,
    Lockpicking,
    Trespassing,
}

// Ids rather than formatted text, so the caller can tell whoever is reading this that it was them.
public record OutstandingCrime(
    DateTime OccurredAt,
    OutstandingCrimeKind Kind,
    string SubjectName,
    Guid? SubjectCreatureId,
    IReadOnlyCollection<string> ItemNames,
    bool IsJailbreak
);

public class GetOutstandingCrimesQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid CityId { get; init; }
    public required int Limit { get; init; }
}

internal class GetOutstandingCrimesQueryHandler(ICrimesDbContext context)
    : IQueryHandler<GetOutstandingCrimesQuery, IReadOnlyList<OutstandingCrime>>
{
    public async Task<IReadOnlyList<OutstandingCrime>> Handle(
        GetOutstandingCrimesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var crimes = await context
            .Crimes.AsNoTracking()
            .Where(crime =>
                crime.WorldId == query.WorldId
                && crime.PlayerId == query.PlayerId
                && crime.CityId == query.CityId
                && crime.Resolution == CrimeResolution.Reported
                && crime.SettledAt == null
            )
            .OrderByDescending(crime => crime.OccurredAt)
            .Take(query.Limit)
            .ToArrayAsync(cancellationToken);

        return crimes.Select(ToOutstandingCrime).ToArray();
    }

    private static OutstandingCrime ToOutstandingCrime(Crime crime) =>
        crime switch
        {
            KillCrime kill => new OutstandingCrime(
                kill.OccurredAt,
                OutstandingCrimeKind.Kill,
                kill.VictimName,
                kill.VictimId,
                [],
                IsJailbreak: false
            ),
            AssaultCrime assault => new OutstandingCrime(
                assault.OccurredAt,
                OutstandingCrimeKind.Assault,
                assault.VictimName,
                assault.VictimId,
                [],
                IsJailbreak: false
            ),
            TheftCrime theft => new OutstandingCrime(
                theft.OccurredAt,
                OutstandingCrimeKind.Theft,
                theft.OwnerName,
                theft.OwnerCreatureId,
                theft.Items.Select(item => item.Name).ToArray(),
                IsJailbreak: false
            ),
            LockpickingCrime breakIn => new OutstandingCrime(
                breakIn.OccurredAt,
                OutstandingCrimeKind.Lockpicking,
                breakIn.BuildingName,
                null,
                [],
                breakIn.IsJailbreak
            ),
            TrespassingCrime trespass => new OutstandingCrime(
                trespass.OccurredAt,
                OutstandingCrimeKind.Trespassing,
                trespass.BuildingName,
                null,
                [],
                IsJailbreak: false
            ),
            _ => throw new InvalidOperationException($"Unhandled crime type {crime.GetType()}"),
        };
}
