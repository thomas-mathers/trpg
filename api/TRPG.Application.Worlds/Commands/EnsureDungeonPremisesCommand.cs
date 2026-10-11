using TRPG.Application.Common.Commands;

namespace TRPG.Application.Worlds.Commands;

[ExemptFromWorldWriteGuard(
    "Saves generated premise text no runner decision reads, and holds an LLM call."
)]
public class EnsureDungeonPremisesCommand
{
    public required IReadOnlyCollection<Guid> BuildingIds { get; init; }
}

// Sequential rather than parallel: EnsureDungeonPremiseCommandHandler shares one scoped
// DbContext, which cannot run concurrent operations.
internal class EnsureDungeonPremisesCommandHandler(
    ICommandHandler<EnsureDungeonPremiseCommand> ensureDungeonPremise
) : ICommandHandler<EnsureDungeonPremisesCommand>
{
    public async Task Handle(
        EnsureDungeonPremisesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var buildingId in command.BuildingIds)
        {
            await ensureDungeonPremise.Handle(
                new EnsureDungeonPremiseCommand { BuildingId = buildingId },
                cancellationToken
            );
        }
    }
}
