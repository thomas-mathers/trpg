using Microsoft.Extensions.AI;
using TRPG.Application.Common.Concurrency;
using TRPG.Application.GameTurns;

namespace TRPG.Tools;

internal sealed class WorldMutationGatedFunction(
    AIFunction inner,
    GameTurnContext turnContext,
    IWorldMutationGate mutationGate
) : DelegatingAIFunction(inner)
{
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken
    )
    {
        await using var lease = await mutationGate.Acquire(turnContext.WorldId, cancellationToken);
        return await base.InvokeCoreAsync(arguments, cancellationToken);
    }
}
