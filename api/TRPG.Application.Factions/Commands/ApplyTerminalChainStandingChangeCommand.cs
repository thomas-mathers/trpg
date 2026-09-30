using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Factions.Commands;

public record ApplyTerminalChainStandingChangeCommand(
    Guid WorldId,
    Guid GiverFactionId,
    Guid AntagonistFactionId
);

internal class ApplyTerminalChainStandingChangeCommandHandler(IFactionsDbContext context)
    : ICommandHandler<ApplyTerminalChainStandingChangeCommand>
{
    private const int StandingPenalty = 5;

    public async Task Handle(
        ApplyTerminalChainStandingChangeCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .FactionStandings.Where(standing =>
                standing.WorldId == command.WorldId
                && standing.Score < 0
                && (
                    (
                        standing.FactionId == command.GiverFactionId
                        && standing.OtherFactionId == command.AntagonistFactionId
                    )
                    || (
                        standing.FactionId == command.AntagonistFactionId
                        && standing.OtherFactionId == command.GiverFactionId
                    )
                )
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        standing => standing.Score,
                        standing => standing.Score - StandingPenalty
                    ),
                cancellationToken
            );
}
