using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class SettleOutstandingCrimesCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid CityId { get; init; }
}

internal class SettleOutstandingCrimesCommandHandler(ICrimesDbContext context)
    : ICommandHandler<SettleOutstandingCrimesCommand>
{
    public async Task Handle(
        SettleOutstandingCrimesCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Crimes.Where(crime =>
                crime.WorldId == command.WorldId
                && crime.PlayerId == command.PlayerId
                && crime.CityId == command.CityId
                && crime.Resolution == CrimeResolution.Reported
                && crime.SettledAt == null
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(crime => crime.SettledAt, DateTime.UtcNow),
                cancellationToken
            );
}
