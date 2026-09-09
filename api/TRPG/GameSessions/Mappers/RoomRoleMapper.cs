using ContractRoomRole = TRPG.GameSessions.Responses.RoomRole;
using DataRoomRole = TRPG.Domain.Models.RoomRole;

namespace TRPG.GameSessions.Mappers;

internal static class RoomRoleMapper
{
    public static ContractRoomRole ToResponse(this DataRoomRole role) =>
        role switch
        {
            DataRoomRole.Entrance => ContractRoomRole.Entrance,
            DataRoomRole.BossChamber => ContractRoomRole.BossChamber,
            DataRoomRole.Passage => ContractRoomRole.Passage,
            DataRoomRole.GuardPost => ContractRoomRole.GuardPost,
            DataRoomRole.Storeroom => ContractRoomRole.Storeroom,
            DataRoomRole.TreasureRoom => ContractRoomRole.TreasureRoom,
            DataRoomRole.Shrine => ContractRoomRole.Shrine,
            DataRoomRole.Study => ContractRoomRole.Study,
            DataRoomRole.CellBlock => ContractRoomRole.CellBlock,
            DataRoomRole.CollapsedGallery => ContractRoomRole.CollapsedGallery,
            DataRoomRole.FloodedSump => ContractRoomRole.FloodedSump,
        };
}
