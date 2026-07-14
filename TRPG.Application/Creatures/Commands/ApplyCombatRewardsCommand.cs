using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Creatures.Commands;

internal class ApplyCombatRewardsCommand
{
    public required Guid CreatureId { get; init; }
    public required int ExperienceGained { get; init; }
    public required int GoldGained { get; init; }
}

internal class ApplyCombatRewardsCommandHandler(TrpgDbContext context)
{
    public async Task Handle(ApplyCombatRewardsCommand command, CancellationToken cancellationToken)
    {
        await context
            .Creatures.Where(c => c.Id == command.CreatureId)
            .ExecuteUpdateAsync(
                c =>
                    c.SetProperty(x => x.Experience, x => x.Experience + command.ExperienceGained)
                        .SetProperty(x => x.Gold, x => x.Gold + command.GoldGained),
                cancellationToken: cancellationToken
            );
    }
}
