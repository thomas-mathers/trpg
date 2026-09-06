using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class SetLockpickingCrimeOutcomeCommand
{
    public required Guid CrimeId { get; init; }
    public LockpickingCrimeOutcome? Outcome { get; init; }
}

internal class SetLockpickingCrimeOutcomeCommandHandler(ICrimesDbContext context)
    : ICommandHandler<SetLockpickingCrimeOutcomeCommand>
{
    public async Task Handle(
        SetLockpickingCrimeOutcomeCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Crimes.OfType<LockpickingCrime>()
            .Where(crime => crime.Id == command.CrimeId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(crime => crime.Outcome, command.Outcome),
                cancellationToken
            );
}
