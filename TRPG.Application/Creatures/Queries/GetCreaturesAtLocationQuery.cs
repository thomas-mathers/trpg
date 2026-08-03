using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal record CreatureSummary(
    Guid Id,
    string Name,
    CreatureType CreatureType,
    Gender Gender,
    Profession? Profession,
    int Level,
    int BirthYear,
    CreatureState State,
    int Gold,
    Guid StateId,
    Guid? DistrictId,
    Guid? RoomId,
    Guid? CityId,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp,
    int Strength,
    int Dexterity,
    int Intelligence,
    int Endurance,
    int Stamina,
    int Mana,
    int Defense,
    float MovementSpeed,
    float PhysicalResistance,
    float FireResistance,
    float IceResistance,
    float LightningResistance,
    float PoisonResistance,
    float MagicResistance
);

internal record CreatureLocation(Guid WorldId, Guid? RoomId, Guid StateId, Guid? DistrictId);

internal static class CreatureLocationFiltering
{
    public static IQueryable<Creature> ApplyFilters(
        IQueryable<Creature> query,
        Guid? excludingCreatureId,
        IReadOnlyCollection<CreatureType>? creatureTypes,
        bool includeDead
    )
    {
        if (excludingCreatureId is not null)
        {
            query = query.Where(p => p.Id != excludingCreatureId);
        }

        if (creatureTypes is not null)
        {
            query = query.Where(p => creatureTypes.Contains(p.CreatureType));
        }

        if (!includeDead)
        {
            query = query.Where(p => p.State != CreatureState.Dead);
        }

        return query;
    }

    public static async Task<IReadOnlyCollection<CreatureSummary>> BuildSummaries(
        TrpgDbContext context,
        IQueryable<Creature> creatureQuery,
        CancellationToken cancellationToken
    )
    {
        var rows = await creatureQuery
            .Select(p => new
            {
                Creature = p,
                Gold = context
                    .Items.OfType<Gold>()
                    .Where(g => g.Ownership.OwnerId == p.Id)
                    .Select(g => (int?)g.Quantity)
                    .FirstOrDefault(),
            })
            .ToArrayAsync(cancellationToken);

        return rows.Select(r => ToCreatureSummary(r.Creature, r.Gold ?? 0)).ToArray();
    }

    private static CreatureSummary ToCreatureSummary(Creature p, int gold) =>
        new(
            p.Id,
            p.Name,
            p.CreatureType,
            p.Gender,
            p.Profession,
            p.Level,
            p.BirthYear,
            p.State,
            gold,
            p.StateId,
            p.DistrictId,
            p.RoomId,
            p.CityId,
            p.CurrentHp,
            p.MaximumHp,
            p.CurrentAp,
            p.MaximumAp,
            p.CurrentMp,
            p.MaximumMp,
            p.Strength,
            p.Dexterity,
            p.Intelligence,
            p.Endurance,
            p.Stamina,
            p.Mana,
            p.Defense,
            p.MovementSpeed,
            p.PhysicalResistance,
            p.FireResistance,
            p.IceResistance,
            p.LightningResistance,
            p.PoisonResistance,
            p.MagicResistance
        );
}

internal class GetCreaturesAtLocationQuery
{
    public required CreatureLocation Location { get; init; }
    public Guid? ExcludingCreatureId { get; init; }
    public IReadOnlyCollection<CreatureType>? CreatureTypes { get; init; }
    public bool IncludeDead { get; init; } = true;
}

internal class GetCreaturesAtLocationQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<CreatureSummary>> Handle(
        GetCreaturesAtLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var location = query.Location;
        var creatureQuery = location.RoomId is { } roomId
            ? context
                .Creatures.AsNoTracking()
                .Where(p =>
                    p.WorldId == location.WorldId
                    && p.StateId == location.StateId
                    && p.RoomId == roomId
                )
            : context
                .Creatures.AsNoTracking()
                .Where(p =>
                    p.WorldId == location.WorldId
                    && p.StateId == location.StateId
                    && p.DistrictId == location.DistrictId
                    && p.RoomId == null
                );

        creatureQuery = CreatureLocationFiltering.ApplyFilters(
            creatureQuery,
            query.ExcludingCreatureId,
            query.CreatureTypes,
            query.IncludeDead
        );

        return await CreatureLocationFiltering.BuildSummaries(
            context,
            creatureQuery,
            cancellationToken
        );
    }
}
