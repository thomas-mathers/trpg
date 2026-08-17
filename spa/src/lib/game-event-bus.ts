import type {
  CombatantState,
  CombatUpdatePayload,
} from '@/api/signalr-client/TRPG.Combat.ClientModels';
import type {
  CharacterLevelUp,
  SkillLevelUp,
} from '@/api/signalr-client/TRPG.Creatures.ClientModels';
import type {
  EncounterResolutionFact,
  HostileEncounterState,
} from '@/api/signalr-client/TRPG.Encounters.Responses';
import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type {
  QuestDialogRequested,
  QuestObjectiveCompleted,
} from '@/api/signalr-client/TRPG.Quests.ClientModels';
import type { TerminalCombatOutcome } from '@/features/combat/combat-outcome';

export type ConnectionStatus = 'connected' | 'reconnecting' | 'reconnected' | 'disconnected';

export type { CharacterLevelUp, QuestDialogRequested, QuestObjectiveCompleted, SkillLevelUp };

interface GameEventMap {
  SceneSnapshot: SceneSnapshot;
  CombatStarted: CombatantState[];
  CombatUpdated: CombatUpdatePayload;
  CombatResolved: TerminalCombatOutcome;
  EncounterStarted: HostileEncounterState;
  EncounterResolved: EncounterResolutionFact;
  SkillLevelUp: SkillLevelUp;
  CharacterLevelUp: CharacterLevelUp;
  QuestDialogRequested: QuestDialogRequested;
  QuestObjectiveCompleted: QuestObjectiveCompleted;
  QuestJournalUpdated: undefined;
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
