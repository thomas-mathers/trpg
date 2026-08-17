using TRPG.Combat.Responses;
using TRPG.Creatures.Responses;
using TRPG.Encounters.Responses;
using TRPG.GameSessions.Responses;
using TRPG.Quests.Responses;
using TypedSignalR.Client;

namespace TRPG.GameSessions.Hubs;

[Receiver]
public interface IGameClient
{
    Task SceneSnapshot(SceneSnapshot snapshot);
    Task CombatStarted(IReadOnlyCollection<CombatantState> combatants);
    Task CombatUpdated(CombatUpdatePayload update);
    Task EncounterStarted(HostileEncounterState encounter);
    Task EncounterResolved(EncounterResolutionFact fact);
    Task SkillLevelUp(SkillLevelUp skillLevelUp);
    Task CharacterLevelUp(CharacterLevelUp characterLevelUp);
    Task QuestDialogRequested(QuestDialogRequested questDialog);
    Task QuestObjectiveCompleted(QuestObjectiveCompleted objective);
    Task QuestJournalUpdated();
}
