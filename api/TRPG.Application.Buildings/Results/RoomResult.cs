using TRPG.Domain.Models;

namespace TRPG.Application.Buildings.Results;

public record RoomResult(
    string RoomName,
    string RoomDescription,
    int RoomFloorNumber,
    Guid BuildingId,
    string BuildingName,
    BuildingType BuildingType,
    string? OwnerName,
    string? FactionName,
    string? FactionDescription
);
