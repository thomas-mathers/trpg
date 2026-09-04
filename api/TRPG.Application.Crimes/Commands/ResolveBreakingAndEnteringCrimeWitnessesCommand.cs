using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public record BreakingAndEnteringCrimeReport(
    Guid CrimeId,
    IReadOnlyCollection<Guid> ReportedWitnessIds
);

public record ResolveBreakingAndEnteringCrimeWitnessesResult(
    IReadOnlyCollection<BreakingAndEnteringCrimeReport> ReportedCrimes
);

public class ResolveBreakingAndEnteringCrimeWitnessesCommand
{
    public required Guid LocationId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> LiveWitnessCreatureIds { get; init; }
}

internal class ResolveBreakingAndEnteringCrimeWitnessesCommandHandler(
    ICrimesDbContext context,
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution
)
    : ICommandHandler<
        ResolveBreakingAndEnteringCrimeWitnessesCommand,
        ResolveBreakingAndEnteringCrimeWitnessesResult
    >
{
    public async Task<ResolveBreakingAndEnteringCrimeWitnessesResult> Handle(
        ResolveBreakingAndEnteringCrimeWitnessesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var resolution = await pendingCrimeWitnessResolution.Resolve<BreakingAndEnteringCrime>(
            command.WorldId,
            command.PlayerId,
            command.LocationId,
            command.LiveWitnessCreatureIds,
            cancellationToken
        );
        if (resolution.Crimes.Count == 0)
        {
            return new ResolveBreakingAndEnteringCrimeWitnessesResult([]);
        }

        await context.SaveChangesAsync(cancellationToken);

        var reportedCrimes = resolution
            .ReportedCrimes.Select(crime => new BreakingAndEnteringCrimeReport(
                crime.Id,
                resolution.ReportingWitnessIdsByCrimeId[crime.Id]
            ))
            .ToArray();

        return new ResolveBreakingAndEnteringCrimeWitnessesResult(reportedCrimes);
    }
}
