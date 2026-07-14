using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Commands;

public class GrantAllAbilitiesCommand
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
    public required IReadOnlyCollection<string> AbilityNames { get; init; }
}

public class GrantAllAbilitiesCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        GrantAllAbilitiesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var existingNames = (
            await context
                .CreatureAbilities.Where(a =>
                    a.WorldId == command.WorldId && a.CreatureId == command.CreatureId
                )
                .Select(a => a.AbilityName)
                .ToArrayAsync(cancellationToken)
        ).ToHashSet();

        var newAbilities = command
            .AbilityNames.Where(name => !existingNames.Contains(name))
            .Select(name => new CreatureAbility
            {
                WorldId = command.WorldId,
                CreatureId = command.CreatureId,
                AbilityName = name,
            });

        context.CreatureAbilities.AddRange(newAbilities);
        await context.SaveChangesAsync(cancellationToken);
    }
}
