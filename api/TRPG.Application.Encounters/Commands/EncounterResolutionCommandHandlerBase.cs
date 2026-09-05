using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public interface IEncounterResolutionCommand
{
    Guid WorldId { get; }
    Guid PlayerId { get; }
    Guid EncounterId { get; }
}

internal abstract class EncounterResolutionCommandHandlerBase<TEncounter, TCommand, TResolution>(
    IEncountersDbContext context
) : ICommandHandler<TCommand, TResolution>
    where TEncounter : Encounter
    where TCommand : IEncounterResolutionCommand
{
    public async Task<TResolution> Handle(
        TCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var encounter = await GetEncounter(command, cancellationToken);

        await CompleteEncounter(command.EncounterId, cancellationToken);

        var resolution = await Resolve(command, encounter, cancellationToken);

        transaction.Complete();

        return resolution;
    }

    protected abstract Task<TResolution> Resolve(
        TCommand command,
        TEncounter encounter,
        CancellationToken cancellationToken
    );

    private async Task<TEncounter> GetEncounter(
        TCommand command,
        CancellationToken cancellationToken
    )
    {
        var encounter =
            await context
                .Encounters.OfType<TEncounter>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item =>
                        item.Id == command.EncounterId
                        && item.WorldId == command.WorldId
                        && item.PlayerId == command.PlayerId,
                    cancellationToken
                )
            ?? throw new EntityNotFoundException(typeof(TEncounter).Name, command.EncounterId);

        if (encounter.State != EncounterState.Active)
        {
            throw new InvalidOperationException("The encounter has already been resolved.");
        }

        return encounter;
    }

    private async Task CompleteEncounter(Guid encounterId, CancellationToken cancellationToken)
    {
        var rowsAffected = await context
            .Encounters.Where(e => e.Id == encounterId && e.State == EncounterState.Active)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(e => e.State, EncounterState.Completed)
                        .SetProperty(e => e.CompletedAt, DateTime.UtcNow),
                cancellationToken
            );

        if (rowsAffected != 1)
        {
            throw new InvalidOperationException(
                $"Encounter {encounterId} is not active and cannot be resolved."
            );
        }
    }
}
