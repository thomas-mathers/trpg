using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.WeaponProficiency.Commands;

public class SetWeaponProficiencyCommand
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
    public required WeaponType WeaponType { get; init; }
    public required int Proficiency { get; init; }
}

public class SetWeaponProficiencyCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        SetWeaponProficiencyCommand command,
        CancellationToken cancellationToken
    )
    {
        var proficiency = await context.CreatureWeaponProficiencies.FirstOrDefaultAsync(
            p =>
                p.WorldId == command.WorldId
                && p.CreatureId == command.CreatureId
                && p.WeaponType == command.WeaponType,
            cancellationToken
        );

        if (proficiency == null)
        {
            context.CreatureWeaponProficiencies.Add(
                new CreatureWeaponProficiency
                {
                    WorldId = command.WorldId,
                    CreatureId = command.CreatureId,
                    WeaponType = command.WeaponType,
                    Proficiency = command.Proficiency,
                }
            );
        }
        else
        {
            proficiency.Proficiency = command.Proficiency;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
