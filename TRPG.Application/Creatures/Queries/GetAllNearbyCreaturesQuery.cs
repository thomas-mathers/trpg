using System.Linq.Expressions;
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
    int Experience,
    int BirthYear,
    CreatureState State,
    int Gold,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp
);

internal record CreatureLocation(Guid WorldId, Guid? RoomId, Guid StateId, Guid? DistrictId);

internal class GetAllNearbyCreaturesQuery
{
    public required CreatureLocation Location { get; init; }
    public Guid? ExcludingCreatureId { get; init; }
    public IReadOnlyCollection<CreatureType>? CreatureTypes { get; init; }
    public bool IncludeDead { get; init; } = true;
}

internal class GetAllNearbyCreaturesQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<CreatureSummary>> Handle(
        GetAllNearbyCreaturesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var location = query.Location;
        return location.RoomId is { } roomId
            ? await GetAllInRoom(
                location.WorldId,
                location.StateId,
                roomId,
                query.ExcludingCreatureId,
                query.CreatureTypes,
                query.IncludeDead,
                cancellationToken
            )
            : await GetAllOutdoorsInLocation(
                location.WorldId,
                location.StateId,
                location.DistrictId,
                query.ExcludingCreatureId,
                query.CreatureTypes,
                query.IncludeDead,
                cancellationToken
            );
    }

    private async Task<IReadOnlyCollection<CreatureSummary>> GetAllInRoom(
        Guid worldId,
        Guid stateId,
        Guid roomId,
        Guid? excludingCreatureId,
        IReadOnlyCollection<CreatureType>? creatureTypes,
        bool includeDead,
        CancellationToken cancellationToken
    )
    {
        var creatureQuery = context
            .Creatures.AsNoTracking()
            .Where(p => p.WorldId == worldId && p.StateId == stateId && p.RoomId == roomId);
        if (excludingCreatureId is not null)
        {
            creatureQuery = creatureQuery.Where(p => p.Id != excludingCreatureId);
        }

        if (creatureTypes is not null)
        {
            creatureQuery = creatureQuery.Where(p => creatureTypes.Contains(p.CreatureType));
        }

        if (!includeDead)
        {
            creatureQuery = creatureQuery.Where(p => p.State != CreatureState.Dead);
        }

        return await creatureQuery.Select(ToCreatureSummary).ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<CreatureSummary>> GetAllOutdoorsInLocation(
        Guid worldId,
        Guid stateId,
        Guid? districtId,
        Guid? excludingCreatureId,
        IReadOnlyCollection<CreatureType>? creatureTypes,
        bool includeDead,
        CancellationToken cancellationToken
    )
    {
        var creatureQuery = context
            .Creatures.AsNoTracking()
            .Where(p =>
                p.WorldId == worldId
                && p.StateId == stateId
                && p.DistrictId == districtId
                && p.RoomId == null
            );
        if (excludingCreatureId is not null)
        {
            creatureQuery = creatureQuery.Where(p => p.Id != excludingCreatureId);
        }

        if (creatureTypes is not null)
        {
            creatureQuery = creatureQuery.Where(p => creatureTypes.Contains(p.CreatureType));
        }

        if (!includeDead)
        {
            creatureQuery = creatureQuery.Where(p => p.State != CreatureState.Dead);
        }

        return await creatureQuery.Select(ToCreatureSummary).ToArrayAsync(cancellationToken);
    }

    private static readonly Expression<Func<Creature, CreatureSummary>> ToCreatureSummary = p =>
        new CreatureSummary(
            p.Id,
            p.Name,
            p.CreatureType,
            p.Gender,
            p.Profession,
            p.Level,
            p.Experience,
            p.BirthYear,
            p.State,
            p.Gold,
            p.CurrentHp,
            p.MaximumHp,
            p.CurrentAp,
            p.MaximumAp,
            p.CurrentMp,
            p.MaximumMp
        );
}
