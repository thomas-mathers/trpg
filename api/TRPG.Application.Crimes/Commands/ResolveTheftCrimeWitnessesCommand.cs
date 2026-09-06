using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Mappers;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public record ResolveTheftCrimeWitnessesResult(IReadOnlyCollection<CrimeReport> ReportedCrimes);

public class ResolveTheftCrimeWitnessesCommand
{
    public required Guid LocationId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> LiveWitnessCreatureIds { get; init; }
}

internal class ResolveTheftCrimeWitnessesCommandHandler(
    ICrimesDbContext context,
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    IOptionsMonitor<ReputationOptions> reputationOptions
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
            command.LiveWitnessCreatureIds,
            cancellationToken
        );
        if (resolution.Crimes.Count == 0)
        {
            return new ResolveTheftCrimeWitnessesResult([]);
        }

        await context.SaveChangesAsync(cancellationToken);

        var options = reputationOptions.CurrentValue;
        var reportedCrimes = resolution
            .ReportedCrimes.Select(crime =>
                crime.ToCrimeReport(resolution.ReportingWitnessIdsByCrimeId[crime.Id], options)
            )
            .ToArray();

        return new ResolveTheftCrimeWitnessesResult(reportedCrimes);
    }
}
