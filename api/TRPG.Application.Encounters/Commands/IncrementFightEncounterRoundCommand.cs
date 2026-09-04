using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

internal class IncrementFightEncounterRoundCommand
{
    public required Guid FightEncounterId { get; init; }
}

internal class IncrementFightEncounterRoundCommandHandler(IEncountersDbContext context)
    : ICommandHandler<IncrementFightEncounterRoundCommand>
{
    public async Task Handle(
        IncrementFightEncounterRoundCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Encounters.OfType<FightEncounter>()
            .Where(f => f.Id == command.FightEncounterId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(f => f.RoundsResolved, f => f.RoundsResolved + 1),
                cancellationToken
            );
    }
}
