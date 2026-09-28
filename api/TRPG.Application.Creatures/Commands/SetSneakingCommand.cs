using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class SetSneakingCommand
{
    public required Guid CreatureId { get; init; }
    public required bool IsSneaking { get; init; }
}

internal class SetSneakingCommandHandler(ICreaturesDbContext context)
    : ICommandHandler<SetSneakingCommand>
{
    public async Task Handle(
        SetSneakingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Creatures.Where(c =>
                c.Id == command.CreatureId
                && (!command.IsSneaking || c.Condition == CreatureCondition.Awake)
            )
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.IsSneaking, command.IsSneaking),
                cancellationToken
            );
    }
}
