using TRPG.Combat.Responses;
using TRPG.Creatures.Responses;
using TRPG.Encounters.Responses;
using TRPG.GameSessions.Hubs;
using TRPG.GameSessions.Responses;
using TRPG.Quests.Responses;

namespace TRPG.Tests.Helpers;

internal sealed class TestGameClient : IGameClient
{
    public Action<SceneSnapshot>? OnSceneSnapshot { get; set; }
    public Action<IReadOnlyCollection<CombatantState>>? OnCombatStarted { get; set; }
    public Action<CombatUpdatePayload>? OnCombatUpdated { get; set; }
    public Action<HostileEncounterState>? OnHostileEncounterStarted { get; set; }
    public Action<HostileEncounterResolutionFact>? OnHostileEncounterResolved { get; set; }
    public Action<GuardEncounterState>? OnGuardEncounterStarted { get; set; }
    public Action<GuardEncounterResolutionFact>? OnGuardEncounterResolved { get; set; }
    public Action<TheftEncounterState>? OnTheftEncounterStarted { get; set; }
    public Action<TheftEncounterResolutionFact>? OnTheftEncounterResolved { get; set; }
    public Action<SkillLevelUp>? OnSkillLevelUp { get; set; }
    public Action<CharacterLevelUp>? OnCharacterLevelUp { get; set; }
    public Action<QuestDialogRequested>? OnQuestDialogRequested { get; set; }
    public Action<QuestObjectiveCompleted>? OnQuestObjectiveCompleted { get; set; }
    public Action? OnQuestJournalUpdated { get; set; }
    public Action<CrimeNotification>? OnCrimeWitnessed { get; set; }
    public Action<CrimeNotification>? OnCrimeWitnessesRemoved { get; set; }

    public Task SceneSnapshot(SceneSnapshot snapshot)
    {
        OnSceneSnapshot?.Invoke(snapshot);
        return Task.CompletedTask;
    }

    public Task CombatStarted(IReadOnlyCollection<CombatantState> combatants)
    {
        OnCombatStarted?.Invoke(combatants);
        return Task.CompletedTask;
    }

    public Task CombatUpdated(CombatUpdatePayload update)
    {
        OnCombatUpdated?.Invoke(update);
        return Task.CompletedTask;
    }

    public Task HostileEncounterStarted(HostileEncounterState encounter)
    {
        OnHostileEncounterStarted?.Invoke(encounter);
        return Task.CompletedTask;
    }

    public Task HostileEncounterResolved(HostileEncounterResolutionFact fact)
    {
        OnHostileEncounterResolved?.Invoke(fact);
        return Task.CompletedTask;
    }

    public Task GuardEncounterStarted(GuardEncounterState encounter)
    {
        OnGuardEncounterStarted?.Invoke(encounter);
        return Task.CompletedTask;
    }

    public Task GuardEncounterResolved(GuardEncounterResolutionFact fact)
    {
        OnGuardEncounterResolved?.Invoke(fact);
        return Task.CompletedTask;
    }

    public Task TheftEncounterStarted(TheftEncounterState encounter)
    {
        OnTheftEncounterStarted?.Invoke(encounter);
        return Task.CompletedTask;
    }

    public Task TheftEncounterResolved(TheftEncounterResolutionFact fact)
    {
        OnTheftEncounterResolved?.Invoke(fact);
        return Task.CompletedTask;
    }

    public Task SkillLevelUp(SkillLevelUp skillLevelUp)
    {
        OnSkillLevelUp?.Invoke(skillLevelUp);
        return Task.CompletedTask;
    }

    public Task CharacterLevelUp(CharacterLevelUp characterLevelUp)
    {
        OnCharacterLevelUp?.Invoke(characterLevelUp);
        return Task.CompletedTask;
    }

    public Task QuestDialogRequested(QuestDialogRequested questDialog)
    {
        OnQuestDialogRequested?.Invoke(questDialog);
        return Task.CompletedTask;
    }

    public Task QuestObjectiveCompleted(QuestObjectiveCompleted objective)
    {
        OnQuestObjectiveCompleted?.Invoke(objective);
        return Task.CompletedTask;
    }

    public Task QuestJournalUpdated()
    {
        OnQuestJournalUpdated?.Invoke();
        return Task.CompletedTask;
    }

    public Task CrimeWitnessed(CrimeNotification notification)
    {
        OnCrimeWitnessed?.Invoke(notification);
        return Task.CompletedTask;
    }

    public Task CrimeWitnessesRemoved(CrimeNotification notification)
    {
        OnCrimeWitnessesRemoved?.Invoke(notification);
        return Task.CompletedTask;
    }
}
