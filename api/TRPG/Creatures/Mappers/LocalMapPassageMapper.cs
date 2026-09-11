using TRPG.Creatures.Queries;
using TRPG.Creatures.Responses;

namespace TRPG.Creatures.Mappers;

internal static class LocalMapPassageMapper
{
    public static LocalMapPassageResponse ToResponse(this LocalMapPassage passage) =>
        new(
            Id: passage.Id,
            OriginRoomId: passage.OriginRoomId,
            DestinationRoomId: passage.DestinationRoomId,
            Path: passage.Path?.Points.Select(point => point.ToMapResponse()).ToArray(),
            IsLocked: passage.IsLocked,
            LockKind: passage.LockKind
        );
}
