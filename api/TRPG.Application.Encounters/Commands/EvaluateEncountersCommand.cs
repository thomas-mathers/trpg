using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateEncountersCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

public record EncounterEvaluationResult(Encounter? Encounter)
{
    public static readonly EncounterEvaluationResult None = new((Encounter?)null);
}

internal class EvaluateEncountersCommandHandler(EncounterEvaluationService encounterEvaluation)
    : ICommandHandler<EvaluateEncountersCommand, EncounterEvaluationResult>
{
    public async Task<EncounterEvaluationResult> Handle(
        EvaluateEncountersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );
        var result = await encounterEvaluation.EvaluateArrival(
            command.WorldId,
            command.PlayerId,
            cancellationToken
        );
        transaction.Complete();
        return result;
    }
}
