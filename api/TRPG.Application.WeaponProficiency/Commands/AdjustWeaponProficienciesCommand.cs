using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.WeaponProficiency.Commands;

public class AdjustWeaponProficienciesCommand
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
    public required IReadOnlyDictionary<WeaponType, int> ProficiencyDeltas { get; init; }
}

public class AdjustWeaponProficienciesCommandHandler(TrpgDbContext context)
    : ICommandHandler<AdjustWeaponProficienciesCommand>
{
    public async Task Handle(
        AdjustWeaponProficienciesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.ProficiencyDeltas.Count == 0)
        {
            return;
        }

        var existingRows = await context
            .CreatureWeaponProficiencies.Where(p =>
                p.WorldId == command.WorldId
                && p.CreatureId == command.CreatureId
                && command.ProficiencyDeltas.Keys.Contains(p.WeaponType)
            )
            .ToDictionaryAsync(p => p.WeaponType, cancellationToken);

        foreach (var (weaponType, delta) in command.ProficiencyDeltas)
        {
            if (existingRows.TryGetValue(weaponType, out var row))
            {
                row.Proficiency += delta;
            }
            else
            {
                context.CreatureWeaponProficiencies.Add(
                    new CreatureWeaponProficiency
                    {
                        WorldId = command.WorldId,
                        CreatureId = command.CreatureId,
                        WeaponType = weaponType,
                        Proficiency = delta,
                    }
                );
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
