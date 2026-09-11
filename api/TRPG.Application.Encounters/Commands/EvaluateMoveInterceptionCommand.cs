using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateMoveInterceptionCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid FromLocationId { get; init; }
    public required Guid ToLocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class EvaluateMoveInterceptionCommandHandler(
    IEncountersDbContext context,
    ICommandHandler<
        EvaluateOverdueRoomKeyEncounterCommand,
        ConfrontOverdueRoomKeyResult
    > evaluateOverdueRoomKeyEncounter,
    EncounterEvaluationService encounterEvaluation
) : ICommandHandler<EvaluateMoveInterceptionCommand, EncounterEvaluationResult>
{
    public async Task<EncounterEvaluationResult> Handle(
        EvaluateMoveInterceptionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );
        var result = await EvaluateInterception(command, cancellationToken);
        if (result.Encounter != null)
        {
            await context
                .Encounters.Where(encounter => encounter.Id == result.Encounter.Id)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            encounter => encounter.DepartureDestinationLocationId,
                            command.ToLocationId
                        ),
                    cancellationToken
                );
        }
        transaction.Complete();
        return result;
    }

    private async Task<EncounterEvaluationResult> EvaluateInterception(
        EvaluateMoveInterceptionCommand command,
        CancellationToken cancellationToken
    )
    {
        var overdueRoomKeyEncounter = await evaluateOverdueRoomKeyEncounter.Handle(
            new EvaluateOverdueRoomKeyEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                FromLocationId = command.FromLocationId,
                ToLocationId = command.ToLocationId,
                Playtime = command.Playtime,
            },
            cancellationToken
        );

        if (overdueRoomKeyEncounter.Encounter != null)
        {
            return new EncounterEvaluationResult(overdueRoomKeyEncounter.Encounter);
        }

        return await encounterEvaluation.EvaluateDeparture(
            command.WorldId,
            command.PlayerId,
            cancellationToken
        );
    }
}
