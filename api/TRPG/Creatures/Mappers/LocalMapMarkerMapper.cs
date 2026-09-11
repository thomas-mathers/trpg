using TRPG.Creatures.Queries;
using TRPG.Creatures.Responses;

namespace TRPG.Creatures.Mappers;

internal static class LocalMapMarkerMapper
{
    public static LocalMapMarkerResponse ToResponse(this LocalMapMarker marker) =>
        new(marker.Id, marker.Name, marker.Kind, marker.State, marker.IsLocked, marker.ItemCount);
}
