using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.WeaponProficiency.Queries;

public class GetWeaponProficienciesByCreatureIdsQuery
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class GetWeaponProficienciesByCreatureIdsQueryHandler(TrpgDbContext context)
    : IQueryHandler<
        GetWeaponProficienciesByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<WeaponType, int>>
    >
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<WeaponType, int>>> Handle(
        GetWeaponProficienciesByCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var rows = await context
            .CreatureWeaponProficiencies.Where(p =>
                p.WorldId == query.WorldId
                && query.CreatureIds.AsEnumerable().Contains(p.CreatureId)
            )
            .Select(p => new
            {
                p.CreatureId,
                p.WeaponType,
                p.Proficiency,
            })
            .ToArrayAsync(cancellationToken);

        var proficienciesByCreature = rows.GroupBy(row => row.CreatureId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(row => row.WeaponType, row => row.Proficiency)
            );

        return query.CreatureIds.ToDictionary(
            creatureId => creatureId,
            creatureId =>
                (IReadOnlyDictionary<WeaponType, int>)
                    Enum.GetValues<WeaponType>()
                        .ToDictionary(
                            type => type,
                            type =>
                                proficienciesByCreature
                                    .GetValueOrDefault(creatureId)
                                    ?.GetValueOrDefault(type)
                                ?? 0
                        )
        );
    }
}
