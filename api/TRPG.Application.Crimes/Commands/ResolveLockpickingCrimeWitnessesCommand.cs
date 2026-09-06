using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public record LockpickingCrimeReport(Guid CrimeId, IReadOnlyCollection<Guid> ReportedWitnessIds);

public record ResolveLockpickingCrimeWitnessesResult(
    IReadOnlyCollection<LockpickingCrimeReport> ReportedCrimes
);

public class ResolveLockpickingCrimeWitnessesCommand
{
    public required Guid LocationId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> LiveWitnessCreatureIds { get; init; }
}

internal class ResolveLockpickingCrimeWitnessesCommandHandler(
    ICrimesDbContext context,
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution
) : ICommandHandler<ResolveLockpickingCrimeWitnessesCommand, ResolveLockpickingCrimeWitnessesResult>
{
    public async Task<ResolveLockpickingCrimeWitnessesResult> Handle(
        ResolveLockpickingCrimeWitnessesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var resolution = await pendingCrimeWitnessResolution.Resolve<LockpickingCrime>(
            command.WorldId,
            command.PlayerId,
            command.LocationId,
            command.LiveWitnessCreatureIds,
            cancellationToken
        );
        if (resolution.Crimes.Count == 0)
        {
            return new ResolveLockpickingCrimeWitnessesResult([]);
        }

        await context.SaveChangesAsync(cancellationToken);

        var reportedCrimes = resolution
            .ReportedCrimes.Select(crime => new LockpickingCrimeReport(
                crime.Id,
                resolution.ReportingWitnessIdsByCrimeId[crime.Id]
            ))
            .ToArray();

        return new ResolveLockpickingCrimeWitnessesResult(reportedCrimes);
    }
}
