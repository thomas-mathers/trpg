using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.WeaponProficiency.Queries;

public class GetAllWeaponProficienciesQuery
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
}

public class GetAllWeaponProficienciesQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetAllWeaponProficienciesQuery, IReadOnlyDictionary<WeaponType, int>>
{
    public async Task<IReadOnlyDictionary<WeaponType, int>> Handle(
        GetAllWeaponProficienciesQuery query,
        CancellationToken cancellationToken
    )
    {
        var existing = await context
            .CreatureWeaponProficiencies.Where(p =>
                p.WorldId == query.WorldId && p.CreatureId == query.CreatureId
            )
            .Select(p => new { p.WeaponType, p.Proficiency })
            .ToDictionaryAsync(k => k.WeaponType, v => v.Proficiency, cancellationToken);

        return Enum.GetValues<WeaponType>()
            .ToDictionary(type => type, type => existing.GetValueOrDefault(type));
    }
}
