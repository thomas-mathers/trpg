using ApplicationQuestMarker = TRPG.Application.Quests.Queries.QuestMarker;
using ContractQuestMarker = TRPG.Contracts.Scenes.Responses.QuestMarker;

namespace TRPG.GameSessions.Mappers;

internal static class QuestMarkerMapper
{
    public static ContractQuestMarker? ToContract(this ApplicationQuestMarker? marker) =>
        marker switch
        {
            ApplicationQuestMarker.Available => ContractQuestMarker.Available,
            ApplicationQuestMarker.ReadyToTurnIn => ContractQuestMarker.ReadyToTurnIn,
            _ => null,
        };
}
