using System.Runtime.ExceptionServices;
using Microsoft.Extensions.AI;
using TRPG.Application.GameTurns;
using TRPG.Application.WorldSimulation;

namespace TRPG.Tools;

internal sealed class WorldRequestFunction(
    AIFunction inner,
    GameTurnContext turnContext,
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
            (_, token) => Invoke(arguments, token),
            cancellationToken
        );

        turnContext.ToolInvocations++;
        invocation.Failure?.Throw();
        return invocation.Result;
    }

    private async Task<ToolInvocation> Invoke(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken
    )
    {
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
