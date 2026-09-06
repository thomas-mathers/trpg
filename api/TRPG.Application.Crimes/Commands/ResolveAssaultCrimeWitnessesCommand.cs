using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public record AssaultCrimeReport(
    Guid VictimId,
    IReadOnlyCollection<Guid> VictimFactionIds,
    IReadOnlyCollection<Guid> ReportedWitnessIds
);

public record ResolveAssaultCrimeWitnessesResult(
    IReadOnlyCollection<AssaultCrimeReport> ReportedCrimes
);

public class ResolveAssaultCrimeWitnessesCommand
{
    public required Guid LocationId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> LiveWitnessCreatureIds { get; init; }
}

internal class ResolveAssaultCrimeWitnessesCommandHandler(
    ICrimesDbContext context,
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution
) : ICommandHandler<ResolveAssaultCrimeWitnessesCommand, ResolveAssaultCrimeWitnessesResult>
{
    public async Task<ResolveAssaultCrimeWitnessesResult> Handle(
        ResolveAssaultCrimeWitnessesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var resolution = await pendingCrimeWitnessResolution.Resolve<AssaultCrime>(
            command.WorldId,
            command.PlayerId,
            command.LocationId,
            command.LiveWitnessCreatureIds,
            cancellationToken
        );
        if (resolution.Crimes.Count == 0)
        {
            return new ResolveAssaultCrimeWitnessesResult([]);
        }

        await context.SaveChangesAsync(cancellationToken);

        var reportedCrimes = resolution
            .ReportedCrimes.Select(crime => new AssaultCrimeReport(
                crime.VictimId,
                crime.VictimFactionIds,
                resolution.ReportingWitnessIdsByCrimeId[crime.Id]
            ))
            .ToArray();

        return new ResolveAssaultCrimeWitnessesResult(reportedCrimes);
    }
}
