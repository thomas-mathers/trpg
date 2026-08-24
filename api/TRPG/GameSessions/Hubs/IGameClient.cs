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
    Task CombatStarted(CombatStartedPayload payload);
    Task CombatUpdated(CombatUpdatePayload update);
    Task HostileEncounterStarted(HostileEncounterState encounter);
    Task HostileEncounterResolved(HostileEncounterResolutionFact fact);
    Task GuardEncounterStarted(GuardEncounterState encounter);
    Task GuardEncounterResolved(GuardEncounterResolutionFact fact);
    Task TheftEncounterStarted(TheftEncounterState encounter);
    Task TheftEncounterResolved(TheftEncounterResolutionFact fact);
    Task SkillLevelUp(SkillLevelUp skillLevelUp);
    Task CharacterLevelUp(CharacterLevelUp characterLevelUp);
    Task QuestDialogRequested(QuestDialogRequested questDialog);
    Task QuestObjectiveCompleted(QuestObjectiveCompleted objective);
    Task QuestJournalUpdated(QuestJournalUpdated questJournal);
    Task CrimeWitnessed(CrimeNotification notification);
    Task CrimeWitnessesRemoved(CrimeNotification notification);
    Task RequestAck(Guid flushId);
}
