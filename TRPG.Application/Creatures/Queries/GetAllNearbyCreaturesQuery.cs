using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

internal record CreatureSummary(
    Guid Id,
    string Name,
    string CreatureTypeName,
    string GenderName,
    string Profession,
    int Level,
    int BirthYear,
    string State
);

internal record CreatureLocation(Guid WorldId, Guid? RoomId, Guid StateId, Guid? DistrictId);

internal class GetAllNearbyCreaturesQuery
{
    public required CreatureLocation Location { get; init; }
    public Guid? ExcludingCreatureId { get; init; }
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
                roomId,
                query.ExcludingCreatureId,
                cancellationToken
            )
            : await GetAllOutdoorsInLocation(
                location.WorldId,
                location.StateId,
                location.DistrictId,
                query.ExcludingCreatureId,
                cancellationToken
            );
    }

    private async Task<IReadOnlyCollection<CreatureSummary>> GetAllInRoom(
        Guid worldId,
        Guid roomId,
        Guid? excludingCreatureId,
        CancellationToken cancellationToken
    )
    {
        var creatureQuery = context
            .Creatures.AsNoTracking()
            .Where(p => p.WorldId == worldId && p.RoomId == roomId);
        if (excludingCreatureId is not null)
        {
            creatureQuery = creatureQuery.Where(p => p.Id != excludingCreatureId);
        }

        return await creatureQuery
            .Select(p => new CreatureSummary(
                p.Id,
                p.Name,
                p.CreatureType.ToString(),
                p.Gender.ToString(),
                p.Profession.ToString()!,
                p.Level,
                p.BirthYear,
                p.State.ToString()
            ))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<CreatureSummary>> GetAllOutdoorsInLocation(
        Guid worldId,
        Guid stateId,
        Guid? districtId,
        Guid? excludingCreatureId,
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

        return await creatureQuery
            .Select(p => new CreatureSummary(
                p.Id,
                p.Name,
                p.CreatureType.ToString(),
                p.Gender.ToString(),
                p.Profession.ToString()!,
                p.Level,
                p.BirthYear,
                p.State.ToString()
            ))
            .ToArrayAsync(cancellationToken);
    }
}
