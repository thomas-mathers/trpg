using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class SetTheftCrimeOutcomeCommand
{
    public required Guid CrimeId { get; init; }
    public TheftCrimeOutcome? Outcome { get; init; }
}

internal class SetTheftCrimeOutcomeCommandHandler(ICrimesDbContext context)
    : ICommandHandler<SetTheftCrimeOutcomeCommand>
{
    public async Task Handle(
        SetTheftCrimeOutcomeCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Crimes.OfType<TheftCrime>()
            .Where(crime => crime.Id == command.CrimeId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(crime => crime.Outcome, command.Outcome),
                cancellationToken
            );
}
