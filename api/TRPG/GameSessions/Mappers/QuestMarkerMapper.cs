using ApplicationQuestMarker = TRPG.Application.Quests.Queries.QuestMarker;
using ApplicationQuestMarkerEntry = TRPG.Application.Quests.Queries.QuestMarkerEntry;
using ContractQuestMarker = TRPG.GameSessions.Responses.QuestMarker;
using ContractQuestMarkerEntry = TRPG.GameSessions.Responses.QuestMarkerEntry;

namespace TRPG.GameSessions.Mappers;

internal static class QuestMarkerMapper
{
    public static ContractQuestMarker ToResponse(this ApplicationQuestMarker marker) =>
        marker switch
        {
            ApplicationQuestMarker.Available => ContractQuestMarker.Available,
            ApplicationQuestMarker.ReadyToTurnIn => ContractQuestMarker.ReadyToTurnIn,
            _ => throw new ArgumentOutOfRangeException(nameof(marker), marker, null),
        };

    public static ContractQuestMarkerEntry ToResponse(this ApplicationQuestMarkerEntry marker) =>
        new(marker.QuestId, marker.Name, marker.Marker.ToResponse());
}
