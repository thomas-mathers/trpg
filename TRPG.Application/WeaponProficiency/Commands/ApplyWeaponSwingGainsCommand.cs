using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.WeaponProficiency.Commands;

public class ApplyWeaponSwingGainsCommand
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
    public required IReadOnlyDictionary<WeaponType, int> SwingCounts { get; init; }
}

public class ApplyWeaponSwingGainsCommandHandler(
    GetAllWeaponProficienciesQueryHandler getAllWeaponProficiencies,
    SetWeaponProficiencyCommandHandler setWeaponProficiency
)
{
    public async Task Handle(
        ApplyWeaponSwingGainsCommand command,
        CancellationToken cancellationToken
    )
    {
        if (command.SwingCounts.Count == 0)
        {
            return;
        }

        var currentProficiencies = await getAllWeaponProficiencies.Handle(
            new GetAllWeaponProficienciesQuery
            {
                WorldId = command.WorldId,
                CreatureId = command.CreatureId,
            },
            cancellationToken
        );

        foreach (var (weaponType, swings) in command.SwingCounts)
        {
            var newProficiency = currentProficiencies.GetValueOrDefault(weaponType) + swings;
            await setWeaponProficiency.Handle(
                new SetWeaponProficiencyCommand
                {
                    WorldId = command.WorldId,
                    CreatureId = command.CreatureId,
                    WeaponType = weaponType,
                    Proficiency = newProficiency,
                },
                cancellationToken
            );
        }
    }
}
