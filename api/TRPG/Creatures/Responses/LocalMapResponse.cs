using TRPG.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Responses;

public record LocalMapRoomResponse(
    Guid Id,
    string Name,
    int FloorNumber,
    RoomBoundsResponse? Bounds,
    RoomRole? Role,
    bool IsVisited,
    bool IsFrontier,
    IReadOnlyList<LocalMapMarkerResponse> Markers
);

public record LocalMapMarkerResponse(
    Guid Id,
    string Name,
    LocalMapMarkerKind Kind,
    LocalMapMarkerState State,
    bool IsLocked,
    int? ItemCount
);

public record RoomBoundsResponse(int Left, int Top, int Right, int Bottom);

public record LocalMapPassageResponse(
    Guid Id,
    Guid OriginRoomId,
    Guid DestinationRoomId,
    IReadOnlyList<PointResponse>? Path,
    bool IsLocked,
    LocalMapLockKind LockKind
);

public record LocalMapResponse(
    Guid BuildingId,
    string BuildingName,
    BuildingType BuildingType,
    Guid CurrentRoomId,
    IReadOnlyList<LocalMapRoomResponse> Rooms,
    IReadOnlyList<LocalMapPassageResponse> Passages
);
