using TRPG.Application.Common.Commands;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.WorldSimulation;
using TRPG.Data;
using TRPG.Worlds.Responses;

namespace TRPG.Worlds.Jobs;

internal sealed class CreateWorldJob(
    ICommandHandler<CreateWorldCommand, CreateWorldResult> handler,
    WorldSimulationCoordinator simulation
)
{
    public async Task<CreateWorldResponse> Run(
        CreateWorldCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await CreateWorld(command, cancellationToken);
        await simulation.EnsureRunner(result.WorldId, cancellationToken);
        return new CreateWorldResponse(result.WorldId, result.PlayerId, result.WorldName);
    }

    private async Task<CreateWorldResult> CreateWorld(
        CreateWorldCommand command,
        CancellationToken cancellationToken
    )
    {
        // No runner has loaded a world that is still being generated.
        using var writeScope = WorldWriteScope.Enter();
        return await handler.Handle(command, cancellationToken);
    }
}
