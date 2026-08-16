using ApplicationQuestMarker = TRPG.Application.Quests.Queries.QuestMarker;
using ContractQuestMarker = TRPG.GameSessions.Responses.QuestMarker;

namespace TRPG.GameSessions.Mappers;

internal static class QuestMarkerMapper
{
    public static ContractQuestMarker? ToResponse(this ApplicationQuestMarker? marker) =>
        marker switch
        {
            ApplicationQuestMarker.Available => ContractQuestMarker.Available,
            ApplicationQuestMarker.ReadyToTurnIn => ContractQuestMarker.ReadyToTurnIn,
            _ => null,
        };
}
