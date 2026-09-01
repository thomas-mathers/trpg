using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.WeaponProficiency.Queries;

public class GetWeaponProficienciesQuery
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
}

internal class GetWeaponProficienciesQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetWeaponProficienciesQuery, IReadOnlyDictionary<WeaponType, int>>
{
    public async Task<IReadOnlyDictionary<WeaponType, int>> Handle(
        GetWeaponProficienciesQuery query,
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
