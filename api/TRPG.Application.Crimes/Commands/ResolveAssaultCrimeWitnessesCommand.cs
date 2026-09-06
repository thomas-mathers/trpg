using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Mappers;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public record ResolveAssaultCrimeWitnessesResult(IReadOnlyCollection<CrimeReport> ReportedCrimes);

public class ResolveAssaultCrimeWitnessesCommand
{
    public required Guid LocationId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> LiveWitnessCreatureIds { get; init; }
}

internal class ResolveAssaultCrimeWitnessesCommandHandler(
    ICrimesDbContext context,
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    IOptionsMonitor<ReputationOptions> reputationOptions
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

        var options = reputationOptions.CurrentValue;
        var reportedCrimes = resolution
            .ReportedCrimes.Select(crime =>
                crime.ToCrimeReport(resolution.ReportingWitnessIdsByCrimeId[crime.Id], options)
            )
            .ToArray();

        return new ResolveAssaultCrimeWitnessesResult(reportedCrimes);
    }
}
