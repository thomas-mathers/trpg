using System.Runtime.ExceptionServices;
using Microsoft.Extensions.AI;
using TRPG.Application.Common.Concurrency;
using TRPG.Application.GameTurns;
using TRPG.Application.WorldSimulation;

namespace TRPG.Tools;

internal sealed class WorldMutationGatedFunction(
    AIFunction inner,
    GameTurnContext turnContext,
    IWorldMutationGate mutationGate,
    WorldSimulationCoordinator simulation
) : DelegatingAIFunction(inner)
{
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken
    )
    {
        var invocation = await simulation.Request(
            turnContext.WorldId,
            (_, token) => InvokeUnderGate(arguments, token),
            cancellationToken
        );

        turnContext.ToolInvocations++;
        invocation.Failure?.Throw();
        return invocation.Result;
    }

    private async Task<ToolInvocation> InvokeUnderGate(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken
    )
    {
        await using var lease = await mutationGate.Acquire(turnContext.WorldId, cancellationToken);

        try
        {
            var result = await base.InvokeCoreAsync(arguments, cancellationToken);
            return new ToolInvocation(result, null);
        }
        catch (Exception exception)
        {
            // A model's bad tool call must not make the runner discard its unwritten events and reload.
            return new ToolInvocation(null, ExceptionDispatchInfo.Capture(exception));
        }
    }

    private sealed record ToolInvocation(object? Result, ExceptionDispatchInfo? Failure);
}
