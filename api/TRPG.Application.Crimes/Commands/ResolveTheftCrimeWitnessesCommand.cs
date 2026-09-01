using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public record TheftCrimeReport(Guid TheftCrimeId, IReadOnlyCollection<Guid> ReportedWitnessIds);

public record ResolveTheftCrimeWitnessesResult(
    IReadOnlyCollection<TheftCrimeReport> ReportedCrimes
);

public class ResolveTheftCrimeWitnessesCommand
{
    public required Guid LocationId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class ResolveTheftCrimeWitnessesCommandHandler(
    TrpgDbContext context,
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution
) : ICommandHandler<ResolveTheftCrimeWitnessesCommand, ResolveTheftCrimeWitnessesResult>
{
    public async Task<ResolveTheftCrimeWitnessesResult> Handle(
        ResolveTheftCrimeWitnessesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var resolution = await pendingCrimeWitnessResolution.Resolve<TheftCrime>(
            command.WorldId,
            command.PlayerId,
            command.LocationId,
            cancellationToken
        );
        if (resolution.Crimes.Count == 0)
        {
            return new ResolveTheftCrimeWitnessesResult([]);
        }

        await context.SaveChangesAsync(cancellationToken);

        var reportedCrimes = resolution
            .ReportedCrimes.Select(crime => new TheftCrimeReport(
                crime.Id,
                resolution.ReportingWitnessIdsByCrimeId[crime.Id]
            ))
            .ToArray();

        return new ResolveTheftCrimeWitnessesResult(reportedCrimes);
    }
}
