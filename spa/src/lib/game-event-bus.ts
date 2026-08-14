import type { CombatantState, SceneSnapshot } from '@/api/client';
import type { TerminalCombatOutcome } from '@/features/combat/combat-outcome';
import type { CombatUpdatePayload } from '@/features/combat/combat-round-event';
import type {
  EncounterResolutionFact,
  HostileEncounterState,
} from '@/features/encounters/encounter';

export type ConnectionStatus = 'connected' | 'reconnecting' | 'reconnected' | 'disconnected';

export interface SkillLevelUp {
  skill: string;
  level: number;
  characterExperienceCurrent: number;
  characterExperienceToNextLevel: number;
}

export interface CharacterLevelUp {
  level: number;
}

export interface QuestDialogRequested {
  worldId: string;
  questId: string;
  name: string;
  description: string;
  goldReward: number;
  objectives: Array<{
    name: string;
    description: string;
    requiredAmount: number;
  }>;
  mode: 'Offer' | 'TurnIn';
}

export interface QuestObjectiveCompleted {
  objectiveName: string;
}

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
