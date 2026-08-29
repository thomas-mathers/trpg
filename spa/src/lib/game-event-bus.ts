import type {
  CombatStartedPayload,
  CombatUpdatePayload,
} from '@/api/signalr-client/TRPG.Combat.Responses';
import type { CharacterLevelUp, SkillLevelUp } from '@/api/signalr-client/TRPG.Creatures.Responses';
import type {
  GuardEncounterResolutionFact,
  GuardEncounterState,
  HostileEncounterResolutionFact,
  HostileEncounterState,
  TheftEncounterResolutionFact,
  TheftEncounterState,
} from '@/api/signalr-client/TRPG.Encounters.Responses';
import type {
  CrimeNotification,
  SceneSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type {
  QuestDialogRequested,
  QuestObjectiveCompleted,
} from '@/api/signalr-client/TRPG.Quests.Responses';
import type { TerminalCombatOutcome } from '@/features/combat/terminal-combat-outcome';

export type ConnectionStatus = 'connected' | 'reconnecting' | 'reconnected' | 'disconnected';

export type { CharacterLevelUp, QuestDialogRequested, QuestObjectiveCompleted, SkillLevelUp };

interface GameEventMap {
  SceneSnapshot: SceneSnapshot;
  CombatStarted: CombatStartedPayload;
  CombatUpdated: CombatUpdatePayload;
  // Fires once the round's animation finishes, so the combat UI, toasts, and respawn flow don't jump ahead of what's on screen.
  CombatResolved: TerminalCombatOutcome;
  // Fires immediately with the round data, before animation — for consumers with nothing to sequence against, like the hidden chat log's marker.
  CombatOutcomeKnown: TerminalCombatOutcome;
  HostileEncounterStarted: HostileEncounterState;
  HostileEncounterResolved: HostileEncounterResolutionFact;
  GuardEncounterStarted: GuardEncounterState;
  GuardEncounterResolved: GuardEncounterResolutionFact;
  TheftEncounterStarted: TheftEncounterState;
  TheftEncounterResolved: TheftEncounterResolutionFact;
  SkillLevelUp: SkillLevelUp;
  CharacterLevelUp: CharacterLevelUp;
  QuestDialogRequested: QuestDialogRequested;
  QuestObjectiveCompleted: QuestObjectiveCompleted;
  QuestJournalUpdated: string | null;
  CrimeWitnessed: CrimeNotification;
  CrimeWitnessesRemoved: CrimeNotification;
  ConnectionStatusChanged: ConnectionStatus;
}

class GameEventBus extends EventTarget {
  emit<K extends keyof GameEventMap>(event: K, detail?: GameEventMap[K]): void {
    if (import.meta.env.DEV && import.meta.env.MODE !== 'test') {
      console.debug(`[gameEventBus] ${event}`, detail);
    }
    this.dispatchEvent(new CustomEvent(event, { detail }));
  }

  on<K extends keyof GameEventMap>(
    event: K,
    listener: (payload: GameEventMap[K]) => void,
  ): () => void {
    const handler = (e: Event) => listener((e as CustomEvent<GameEventMap[K]>).detail);
    this.addEventListener(event, handler);
    return () => this.removeEventListener(event, handler);
  }
}

export const gameEventBus = new GameEventBus();
