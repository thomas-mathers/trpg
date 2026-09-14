using TRPG.Inventory.Responses;

namespace TRPG.Quests.Responses;

public record DeliverItemDialogResponse(Guid QuestId, string QuestName, ItemDetail Item);
