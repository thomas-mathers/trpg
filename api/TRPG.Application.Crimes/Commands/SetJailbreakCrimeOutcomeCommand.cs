using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class SetJailbreakCrimeOutcomeCommand
{
    public required Guid CrimeId { get; init; }
    public LockpickingCrimeOutcome? Outcome { get; init; }
}

internal class SetJailbreakCrimeOutcomeCommandHandler(ICrimesDbContext context)
    : ICommandHandler<SetJailbreakCrimeOutcomeCommand>
{
    public async Task Handle(
        SetJailbreakCrimeOutcomeCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Crimes.OfType<JailbreakCrime>()
            .Where(crime => crime.Id == command.CrimeId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(crime => crime.Outcome, command.Outcome),
                cancellationToken
            );
}
