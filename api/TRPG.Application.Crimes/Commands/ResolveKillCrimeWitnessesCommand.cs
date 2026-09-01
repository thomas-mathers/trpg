using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public record KillCrimeReport(Guid VictimId, IReadOnlyCollection<Guid> ReportedWitnessIds);

public record ResolveKillCrimeWitnessesResult(IReadOnlyCollection<KillCrimeReport> ReportedCrimes);

public class ResolveKillCrimeWitnessesCommand
{
    public required Guid LocationId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class ResolveKillCrimeWitnessesCommandHandler(
    TrpgDbContext context,
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution
) : ICommandHandler<ResolveKillCrimeWitnessesCommand, ResolveKillCrimeWitnessesResult>
{
    public async Task<ResolveKillCrimeWitnessesResult> Handle(
        ResolveKillCrimeWitnessesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var resolution = await pendingCrimeWitnessResolution.Resolve<KillCrime>(
            command.WorldId,
            command.PlayerId,
            command.LocationId,
            cancellationToken
        );
        if (resolution.Crimes.Count == 0)
        {
            return new ResolveKillCrimeWitnessesResult([]);
        }

        await context.SaveChangesAsync(cancellationToken);

        var reportedCrimes = resolution
            .ReportedCrimes.Select(crime => new KillCrimeReport(
                crime.VictimId,
                resolution.ReportingWitnessIdsByCrimeId[crime.Id]
            ))
            .ToArray();

        return new ResolveKillCrimeWitnessesResult(reportedCrimes);
    }
}
