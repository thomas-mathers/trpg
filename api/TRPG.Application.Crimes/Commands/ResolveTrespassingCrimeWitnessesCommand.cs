using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Mappers;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public record ResolveTrespassingCrimeWitnessesResult(
    IReadOnlyCollection<CrimeReport> ReportedCrimes
);

public class ResolveTrespassingCrimeWitnessesCommand
{
    public required Guid LocationId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> LiveWitnessCreatureIds { get; init; }
}

internal class ResolveTrespassingCrimeWitnessesCommandHandler(
    ICrimesDbContext context,
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICommandHandler<ResolveTrespassingCrimeWitnessesCommand, ResolveTrespassingCrimeWitnessesResult>
{
    public async Task<ResolveTrespassingCrimeWitnessesResult> Handle(
        ResolveTrespassingCrimeWitnessesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var resolution = await pendingCrimeWitnessResolution.Resolve<TrespassingCrime>(
            command.WorldId,
            command.PlayerId,
            command.LocationId,
            command.LiveWitnessCreatureIds,
            cancellationToken
        );
        if (resolution.Crimes.Count == 0)
        {
            return new ResolveTrespassingCrimeWitnessesResult([]);
        }

        await context.SaveChangesAsync(cancellationToken);

        var options = reputationOptions.CurrentValue;
        var reportedCrimes = resolution
            .ReportedCrimes.Select(crime =>
                crime.ToCrimeReport(resolution.ReportingWitnessIdsByCrimeId[crime.Id], options)
            )
            .ToArray();

        return new ResolveTrespassingCrimeWitnessesResult(reportedCrimes);
    }
}
