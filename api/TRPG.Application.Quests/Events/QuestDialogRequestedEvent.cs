using TRPG.Application.Common.Events;
using TRPG.Application.Quests.Results;

namespace TRPG.Application.Quests.Events;

public enum QuestDialogMode
{
    Offer,
    TurnIn,
}

public record QuestDialogRequestedEvent(
    Guid WorldId,
    QuestConversationResult Quest,
    QuestDialogMode Mode
) : GameClientEvent;
